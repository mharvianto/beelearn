# BeeLearn — Panduan Instalasi

Papan gaya Padlet + online judge C/C++. Backend ASP.NET Core 10, frontend Vue 3
(di-build ke `wwwroot/`), database SQLite, realtime SignalR.

---

## 1. Prasyarat

| Komponen | Versi | Catatan |
|---|---|---|
| **.NET SDK** | **10.0** | `dotnet --version` → `10.0.x` |
| **Node.js + npm** | **20+** (diuji dengan 24) | untuk membangun SPA Vue |
| **gcc & g++** | 11+ (diuji 13) | **wajib di PATH** — judge meng-compile kode C/C++ murid |
| **OS** | **Linux** | Judge memakai `fork` + `setrlimit` (helper C khusus POSIX). Di Windows/macOS backend tetap jalan, tapi *Run/Submit* tidak. |
| bubblewrap (`bwrap`) | opsional | isolasi filesystem/jaringan. Tanpa ini → mode *rlimits-only* (limit waktu & memori tetap dipaksakan). |

Yang **tidak perlu** dipasang:

- Font — DejaVu TTF sudah disertakan di `BeeLearn/Assets/fonts/` (dipakai untuk render soal-terenkripsi).
- Native SkiaSharp — paket `SkiaSharp.NativeAssets.Linux.NoDependencies` sudah membundel binari (jalan di Ubuntu 24.04 tanpa dependensi tambahan).

### Pasang prasyarat di Ubuntu/Debian

```bash
# .NET 10 SDK
sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0    # atau lewat https://dot.net

# Node.js 20+ (contoh via nodesource)
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash - && sudo apt-get install -y nodejs

# toolchain judge
sudo apt-get install -y build-essential

# opsional: sandbox filesystem
sudo apt-get install -y bubblewrap
```

---

## 2. Ambil kode

```bash
git clone <URL-repo> beelearn
cd beelearn
```

---

## 3. Menjalankan untuk pengembangan (dua terminal)

### Terminal 1 — backend

```bash
cd BeeLearn
dotnet run
```

- Mendengarkan di **http://localhost:5048** (profil `http` di `Properties/launchSettings.json`).
- Saat pertama kali: `beelearn.db` dibuat, migrasi dijalankan, lalu data demo + **bank 81 soal** di-seed otomatis.
- Startup akan mencetak mode sandbox, mis.
  `Sandbox mode: rlimits only (bwrap ...)` atau `bubblewrap + rlimits`.

### Terminal 2 — frontend (Vite dev server)

```bash
cd BeeLearn/ClientApp
npm install
npm run dev
```

- Mendengarkan di **http://localhost:5173** dan mem-proxy `/api` + `/hubs` ke `http://localhost:5048`.
- Kalau backend memakai port lain: `BACKEND_URL=http://localhost:5001 npm run dev`.

Buka **http://localhost:5173**.

### Akun demo

| Peran | Kredensial |
|---|---|
| Guru | `teacher@demo.test` / `password` |
| Board demo | `/boards/demo-board` — kode gabung **`DEMO01`** |
| Murid | daftar sendiri dari halaman **Register** |

---

## 4. Build satu proses (produksi)

```bash
cd BeeLearn
dotnet publish -c Release -o out
```

- Target MSBuild `BuildClientApp` otomatis menjalankan `npm ci && npm run build` ke `wwwroot/`.
  Lewati dengan `-p:BuildClient=false` bila SPA sudah kamu build sendiri.

Jalankan:

```bash
cd out
ASPNETCORE_URLS="http://0.0.0.0:8080" \
ASPNETCORE_ENVIRONMENT=Production \
./BeeLearn
```

- Satu port melayani SPA + REST API + SignalR (`app.MapFallbackToFile("index.html")` menangani route klien).
- Taruh **reverse proxy** (nginx/Caddy) di depan untuk TLS dan **upgrade WebSocket** (dibutuhkan SignalR).

Contoh lokasi nginx:

```nginx
location / {
    proxy_pass         http://127.0.0.1:8080;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection "upgrade";
    proxy_set_header   Host $host;
    proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;
}
```

---

## 5. Konfigurasi

Ubah lewat `BeeLearn/appsettings.json`, `appsettings.Production.json`, atau environment
variable (nesting pakai `__`).

| Kunci | Default | Keterangan |
|---|---|---|
| `ConnectionStrings:Default` | `Data Source=beelearn.db` | Path file SQLite. Produksi: `Data Source=/var/lib/beelearn/beelearn.db`. |
| `Judge:WorkRoot` | `/tmp/beelearn-judge` | Direktori kerja compile/run. |
| `Judge:MaxConcurrent` | `2` | Jumlah compile/run paralel. |
| `Judge:CompileTimeoutMs` | `10000` | Batas waktu kompilasi. |
| `Judge:QueueCapacity` | `200` | Kapasitas antrean judge. |
| `Judge:HardWallBufferMs` | `800` | Selisih wall-clock di atas batas soal sebelum di-*kill* paksa. |
| `Judge:RunTimeLimitMs` | `1000` | Batas waktu default tombol **Run** ad-hoc. |
| `Judge:RunMemoryLimitKb` | `32768` | Batas memori default tombol **Run**. |
| `Judge:RateLimitMs` | `1500` | Jarak minimum antar Run/Submit per user. |
| `ASPNETCORE_URLS` | — | mis. `http://0.0.0.0:8080`. |

Contoh override via env:

```bash
export ConnectionStrings__Default="Data Source=/var/lib/beelearn/beelearn.db"
export Judge__MaxConcurrent=4
export Judge__WorkRoot=/var/tmp/beelearn-judge
```

---

## 6. Catatan sandbox / keamanan judge

- **Limit waktu & memori selalu dipaksakan** lewat `setrlimit` (CPU, address space, stack,
  jumlah proses, ukuran file) + backstop wall-clock.
- **Isolasi filesystem/jaringan hanya aktif bila `bwrap` bisa membuat namespace.** Itu perlu
  *unprivileged user namespaces* diizinkan host:
  - Debian/lama: `sudo sysctl -w kernel.unprivileged_userns_clone=1`
  - Ubuntu 24.04 (dibatasi AppArmor): `sudo sysctl -w kernel.apparmor_restrict_unprivileged_userns=0`
  - Kontainer Docker: jalankan dengan `--security-opt seccomp=unconfined --security-opt apparmor=unconfined`
    (dev container bawaan repo **memblokir** userns → judge berjalan *rlimits-only*).
- Untuk produksi: jalankan aplikasi sebagai **user berprivilege rendah** khusus, dan idealnya
  pisahkan eksekusi kode ke VM/host sekali-pakai. Lihat bagian *Security note* di `README.md`.

---

## 7. Migrasi database (opsional)

Aplikasi **otomatis menerapkan migrasi** saat startup. Untuk mengelolanya manual:

```bash
dotnet tool install --global dotnet-ef      # sekali saja
export PATH="$PATH:$HOME/.dotnet/tools"

cd BeeLearn
dotnet ef migrations add NamaMigrasi
dotnet ef database update
```

**Reset data pengembangan:** hentikan aplikasi, `rm BeeLearn/beelearn.db*`, jalankan lagi →
seed ulang (demo + 81 soal).

---

## 8. Ringkasan port

| Port | Dipakai |
|---|---|
| 5048 | Backend Kestrel (dev, profil `http`) |
| 7101 | Backend Kestrel (dev, profil `https`) |
| 5173 | Vite dev server (frontend) |
| 8080 | Contoh port produksi (`ASPNETCORE_URLS`) |

---

## 9. Dev Container (VS Code)

Repo menyertakan `.devcontainer/devcontainer.json` (image .NET 10 + fitur Node).

1. Buka folder di VS Code → **Reopen in Container**.
2. Ikuti langkah **Bagian 3** (dua terminal).
3. Catatan: base image ini memblokir user namespaces → judge memakai mode *rlimits-only*.

---

## 10. Troubleshooting

| Gejala | Solusi |
|---|---|
| Startup gagal: `gcc not found on PATH` / `g++ not found` | `sudo apt-get install -y build-essential` |
| Buka `:5048` langsung → halaman kosong / 404 | Jalankan `npm run build` di `ClientApp` (mengisi `wwwroot/`), atau pakai Vite di `:5173`. |
| SignalR tidak connect di belakang proxy | Teruskan header `Upgrade`/`Connection` untuk WebSocket; pastikan path `/hubs` ikut ter-proxy. |
| Verdict selalu `MemoryLimit` untuk program sederhana | Naikkan `MemoryLimitKb` pada soal (mis. 65536) — `RLIMIT_AS` membatasi *virtual address space*, bukan RSS. |
| Gambar soal terlindungi kosong / error | Pada image minimal, `sudo apt-get install -y libfontconfig1`. Di Ubuntu 24.04 biasanya sudah cukup. |
| `Address already in use` | Ubah `applicationUrl` di `launchSettings.json` atau set `ASPNETCORE_URLS`, dan sesuaikan `BACKEND_URL` untuk Vite. |
| Judge lambat / antre | Naikkan `Judge__MaxConcurrent` sesuai jumlah core. |
