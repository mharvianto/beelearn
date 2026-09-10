# BeeCoding — Deployment: Azure Web App, Kubernetes, Scale-out

Panduan ini melengkapi [INSTALL.md](INSTALL.md) (deploy single-VM). Isinya jawaban untuk
tiga pertanyaan:

1. Bisakah deploy di **Azure Web App (App Service)**?
2. Apa yang harus disiapkan untuk **Kubernetes**?
3. Apa yang harus diubah untuk **scale-out** (lebih dari 1 replika)?

> **Ringkas:** BeeCoding saat ini **didesain single-node** — semua state realtime ada di
> memori proses, database SQLite berupa file, dan judge berjalan **in-process**. Deploy
> managed / multi-replika membutuhkan perubahan arsitektur, bukan sekadar konfigurasi.

---

## 0. Peta state & komponen (kenapa scale-out butuh kerja)

| Komponen | Implementasi sekarang | Masalah saat > 1 replika |
|---|---|---|
| Database | **SQLite** file `app.db`, migrasi otomatis saat boot | File lock, korupsi kalau ditulis banyak pod; migrasi balapan saat pod boot bareng |
| Realtime | **SignalR** in-memory (tanpa backplane) | Pesan dari pod A tidak sampai ke klien di pod B |
| Live draft / lecture / presence | `DraftStore`, `LectureStore`, `PresenceTracker` — singleton memori | Murid di pod A & guru di pod B tidak saling lihat |
| Job generate/regenerate soal AI | `AiGenerationJobs` — `ConcurrentDictionary` memori | Polling `GET /api/ai/generate-problem/{id}` bisa kena pod yang tidak punya job itu |
| Judge | `JudgeQueue` = `Channel<JudgeJob>` in-process, `JudgeWorker` `BackgroundService` | Kode murid jalan di container web; tiap pod web butuh toolchain + sandbox |
| Throttle / cache | `LoginThrottle`, `RateLimiter`, throttle AI, cache rekomendasi — memori | Limit jadi per-pod (lebih longgar); cache tidak dibagi |
| Cookie auth | Data Protection keys **default** (folder efemeral per proses) | Restart pod / pod baru → semua logout; cookie pod A ditolak pod B |
| clangd LSP | 1 proses clangd per koneksi `/lsp/cpp`, ~1–2 GB RAM | Stateful, rakus memori; perlu routing sticky |
| Aset statis | Di-serve oleh app, sudah ber-hash + `Cache-Control: immutable` + brotli | Aman apa adanya (tiap pod serve bundle-nya sendiri) |

Konfigurasi sudah bisa lewat **environment variable** (`Section__Key`, mis. `Ai__ApiKey`,
`Judge__MaxConcurrent`, `ConnectionStrings__Default`).

---

## 1. Azure Web App (App Service)

**Bisa, dengan custom container, tapi realistis hanya untuk 1 instance / kelas kecil.**

### Batasan

| Aspek | Kenyataan di App Service (Linux) |
|---|---|
| **Judge** | App Service adalah PaaS ter-sandbox: tidak ada `apt` persisten, bukan root, **tidak ada user namespace** → `bubblewrap` **tidak jalan**. Harus pakai **Web App for Containers** dengan image berisi `gcc`/`g++`. Judge jadi **rlimits-only**, kode murid jalan di dalam container web-mu — sesuai catatan keamanan README ("classroom-grade"), bukan isolasi multi-tenant. |
| **SQLite** | File di `/home` (Azure Files) — latensi & locking buruk untuk SQLite, **rusak kalau instance > 1**. OK untuk satu instance Always On lalu-lintas rendah. |
| **WebSocket** (SignalR, `/lsp`) | Didukung. Aktifkan **Configuration → General settings → Web sockets = On**. **ARR affinity** default On (SignalR butuh sticky tanpa backplane). |
| **Background services** (`JudgeWorker`, `JudgeJanitor`) | Wajib **Always On = On** (kalau tidak, proses di-idle-out). |
| **clangd** | Perlu binary di image, ~1–2 GB RAM per sesi → plan besar, atau `Lsp__Enabled=false`. |
| **Data Protection keys** | Default = per-instance efemeral → restart = semua logout. Persist ke Blob + lindungi dengan Key Vault (lihat §3.3). |
| **Outbound ke endpoint AI** | Tidak masalah. |

### Kalau tetap mau App Service (1 instance)

1. `Web App for Containers`, image kustom (lihat §2.7 Dockerfile).
2. `Always On = On`, `Web sockets = On`, `ARR affinity = On`.
3. Plan minimal **P1v3** (judge + clangd butuh RAM).
4. App settings (env): `Ai__ApiKey`, `Auth__TeacherSignupCode`, `Admin__Token`, `Lsp__Enabled`, `Judge__MaxConcurrent`, dst.
5. SQLite di `/home/data/app.db` (persisten antar restart): `ConnectionStrings__Default=Data Source=/home/data/app.db`. **Jangan** naikkan instance count.
6. Data Protection → Blob + Key Vault.
7. **Scale-up**, bukan scale-out.

> **Alternatif yang lebih pas di Azure:** **Azure Container Apps** — ingress + WebSocket
> bawaan, KEDA autoscale, lebih longgar dari App Service. Tapi untuk > 1 replika tetap perlu
> semua externalisasi state di §3, dan isolasi judge tetap tanggung jawabmu.

---

## 2. Persiapan Kubernetes (AKS)

Arahnya: **pecah jadi dua tier** dan pindahkan semua state ke layanan bersama.

### 2.1 Tier

- **`beecoding-web`** — Deployment N replika, **stateless**. Serve SPA + REST + hub SignalR.
- **`beecoding-judge`** — Deployment terpisah. Satu-satunya yang butuh `gcc`/`g++` + sandbox
  kuat. Ambil job dari broker, kembalikan hasil lewat broker.

### 2.2 Database → PostgreSQL

- Pakai **Azure Database for PostgreSQL Flexible Server** (atau operator in-cluster).
- Ganti provider: `Npgsql.EntityFrameworkCore.PostgreSQL`, sesuaikan `AddDbContext`, **regen
  migrations** (SQLite dan Npgsql beda tipe kolom).
- Connection string via Secret → env `ConnectionStrings__Default`.
- **Migrasi**: jangan andalkan `db.Database.Migrate()` saat boot (N pod balapan). Jalankan
  sebagai **init container** atau **Job** `dotnet BeeCoding.dll migrate` sekali, gate rollout
  dengan itu.

### 2.3 SignalR backplane

Salah satu:

- **Azure SignalR Service** (mode *Default*) — paling sederhana, hub di-offload, **tidak
  perlu sticky session**. Tambah `Azure.SignalR` + `AddSignalR().AddAzureSignalR(conn)`.
- **Redis backplane** — `Microsoft.AspNetCore.SignalR.StackExchangeRedis` +
  `AddStackExchangeRedis(conn)`. Lebih murah, tapi masih ingin sticky untuk urutan
  negotiate→WS (atau pakai `skipNegotiation` + transport `WebSockets`).

### 2.4 State memori → Redis

Ganti singleton berikut dengan implementasi yang didukung Redis (Azure Cache for Redis):

- `DraftStore`, `LectureStore`, `PresenceTracker` → hash/key Redis dengan TTL.
- `AiGenerationJobs` → key Redis (atau tabel `ai_jobs`) supaya polling job kena pod mana pun.
- `LoginThrottle`, `RateLimiter`, throttle AI (`AiController._last`), cache rekomendasi
  (`PracticeController._aiCache`) → Redis, atau terima jadi per-pod (limit lebih longgar).

> Hack sementara: jadikan hub + fitur realtime satu Deployment **1-replika**, sisanya
> di-scale. Ini hanya memindahkan bottleneck — bukan solusi.

### 2.5 Data Protection keys (cookie auth)

Wajib **shared + persisten**, kalau tidak tiap pod baru meng-invalidate cookie semua orang:

```csharp
builder.Services.AddDataProtection()
    .SetApplicationName("beecoding")
    .PersistKeysToStackExchangeRedis(redis, "beecoding:dp-keys");
    // atau .PersistKeysToAzureBlobStorage(...).ProtectKeysWithAzureKeyVault(...)
```

### 2.6 Judge di Kubernetes

Jangan jalankan binary murid di pod biasa.

- **Isolasi**: `RuntimeClass` **gVisor (`runsc`)** atau **Kata Containers**, atau
  container rootless per-submission. `bubblewrap` butuh `allowPrivilegeEscalation` / user
  namespace — beri `securityContext` longgar **hanya** di pod judge, atau pakai gVisor dan
  lepas bwrap.
- **NetworkPolicy**: pod judge `egress: deny-all`.
- **Scratch**: `Judge__WorkRoot` → `emptyDir` (boleh `medium: Memory`), **bukan** PVC.
- **Antrian**: ganti `JudgeQueue` (`Channel<JudgeJob>`) dengan broker — **Redis Streams**,
  RabbitMQ, atau **Azure Service Bus**. `JudgeWorker` consume dari broker; hasil di-publish
  balik → `beecoding-web` relay lewat SignalR.
- **Autoscale**: HPA/**KEDA** berdasarkan panjang antrian broker. `Judge__MaxConcurrent` per
  pod = jumlah core yang dialokasikan.
- **Graceful shutdown**: `preStop` + `IHostApplicationLifetime` untuk menuntaskan job
  in-flight sebelum pod mati.

### 2.7 clangd LSP

- Untuk skala: **matikan** (`Lsp__Enabled=false`).
- Atau: pool pod sendiri (`beecoding-lsp`) dengan memory limit besar + `Lsp__MaxConcurrent`,
  route `/lsp/*` ke sana. Koneksi WS stateful → routing per-koneksi sticky.

### 2.8 Ingress

- Aktifkan WebSocket.
- `/hubs` & `/lsp`: `proxy-read-timeout: 3600`, `proxy-send-timeout: 3600`.
- `/api/ai/`: `proxy_buffering off` (SSE `hint/stream`), `proxy-read-timeout: 600`.
- Body size: `/api/` ≥ 20m (edit soal bisa beberapa MB), `/api/admin/` 100m.
- Contoh timeout/limit ada di [INSTALL.md](INSTALL.md) bagian nginx (anotasi ingress-nginx setara).

### 2.9 ForwardedHeaders

`Program.cs` sekarang `KnownIPNetworks.Clear()` (percaya proxy langsung). Di K8s, set
`KnownNetworks` ke CIDR pod/ingress supaya `X-Forwarded-Proto` dipercaya (Secure cookie +
HSTS aktif hanya kalau app tahu request-nya HTTPS).

### 2.10 Secrets

`Ai__ApiKey`, `Auth__TeacherSignupCode`, `Admin__Token`, connection string DB & Redis →
**K8s Secret** atau **Azure Key Vault + CSI Secrets Store driver**, di-inject sebagai env.
`appsettings.json` di image tetap kosong untuk field-field ini.

### 2.11 Health checks & observability

- Tambah `builder.Services.AddHealthChecks()` + `app.MapHealthChecks("/health")` (cek DB +
  Redis). `livenessProbe` = `/health`, `readinessProbe` menunggu DB & Redis siap.
- Log JSON ke stdout (bukan journald); OpenTelemetry / Application Insights.

### 2.12 Image (Dockerfile multi-stage)

```dockerfile
# --- build SPA + publish ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && apt-get install -y nodejs
WORKDIR /src
COPY . .
RUN dotnet publish BeeCoding/BeeCoding.csproj -c Release -o /app   # menjalankan npm ci && npm run build

# --- runtime web ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web
RUN apt-get update && apt-get install -y --no-install-recommends gcc g++ bubblewrap \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "BeeCoding.dll"]
```

Pod judge: image sama (butuh toolchain), dijalankan dengan `RuntimeClass: gvisor` dan
`Judge__Role=worker` (kalau nanti ada flag pemisah web/worker). Tambah `clangd` ke image
hanya bila LSP dipakai.

---

## 3. Perubahan minimum untuk scale-out (> 1 replika web)

Urutan prioritas — **tanpa 1–4, replika kedua langsung merusak data / memutus realtime:**

1. **DB relasional** (PostgreSQL) + migrasi via Job/init-container.
2. **SignalR backplane** — Azure SignalR Service (paling mudah) atau Redis.
3. **Data Protection keys** shared (Redis / Blob+Key Vault) + `SetApplicationName`.
4. **Externalisasi state memori** ke Redis: `DraftStore`, `LectureStore`, `PresenceTracker`,
   `AiGenerationJobs`, throttle & cache.
5. **Judge → broker + worker pool terpisah** dengan isolasi gVisor/Kata; hasil dikembalikan
   lewat broker.
6. **Sticky session OFF** setelah backplane ada (atau `skipNegotiation` + WS).
7. **clangd**: `Lsp__Enabled=false`, atau pool khusus dengan routing sticky.
8. **Health checks** + probe + graceful shutdown (drain job judge in-flight).
9. **Logging** JSON ke stdout + OpenTelemetry / Application Insights.
10. Aset statis: aman apa adanya; CDN di depan bila mau.

### Tambahan kode yang diperlukan (belum ada di repo)

- Abstraksi `IDraftStore`/`ILectureStore`/`IPresenceTracker` + implementasi Redis.
- `IJudgeQueue` di atas broker (publish job + subscribe hasil) menggantikan `Channel<>`.
- `IAiJobStore` di atas Redis menggantikan `AiGenerationJobs`.
- Endpoint `/health` + probe.
- Migrasi mode "run-and-exit" (`dotnet BeeCoding.dll migrate`).

---

## 4. Untuk kondisi sekarang (1 kelas ~40 murid)

**Tidak perlu semua di atas.** VM 4 vCPU + tuning:

- `Judge__MaxConcurrent=2`, swap aktif, `DOTNET_gcServer=0` (workstation GC), `Judge__WorkRoot`
  di disk lokal, `Lsp__Enabled=false`.
- nginx: lihat [INSTALL.md](INSTALL.md).

Refactor scale-out baru relevan kalau sudah banyak kelas paralel atau butuh HA —
**scale up dulu, bukan scale out.**
