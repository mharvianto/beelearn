# BeeLearn — Panduan Instalasi

Papan gaya Padlet + online judge C/C++. Backend ASP.NET Core 10, frontend Vue 3
(di-build ke `wwwroot/`), database SQLite, realtime SignalR.

---

## 1. Prasyarat

| Komponen | Versi | Catatan |
|---|---|---|
| **.NET SDK** | **10.0** | `dotnet --version` → `10.0.x`. Belum di repo semua distro — lihat cara pasang di bawah. |
| **Node.js + npm** | **20+** (diuji dengan 24) | untuk membangun SPA Vue |
| **gcc & g++** | 11+ (diuji 13) | **wajib di PATH** — judge meng-compile kode C/C++ murid |
| **OS** | **Linux** | Judge memakai `fork` + `setrlimit` (helper C khusus POSIX). Di Windows/macOS backend tetap jalan, tapi *Run/Submit* tidak. |
| bubblewrap (`bwrap`) | opsional | isolasi filesystem/jaringan. Tanpa ini → mode *rlimits-only* (limit waktu & memori tetap dipaksakan). |

Yang **tidak perlu** dipasang:

- Font — DejaVu TTF sudah disertakan di `BeeLearn/Assets/fonts/` (dipakai untuk render soal-terenkripsi).
- Native SkiaSharp — paket `SkiaSharp.NativeAssets.Linux.NoDependencies` sudah membundel binari (jalan di Ubuntu 24.04 tanpa dependensi tambahan).

### Pasang prasyarat di Ubuntu/Debian

**.NET 10 SDK** — paket `dotnet-sdk-10.0` belum tentu ada di repo distro
(mis. Debian 13/trixie belum). Cara paling andal adalah script resmi Microsoft:

```bash
sudo apt-get update && sudo apt-get install -y libicu-dev ca-certificates
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 10.0            # → $HOME/.dotnet

# tambahkan ke ~/.bashrc agar permanen:
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$PATH:$HOME/.dotnet:$HOME/.dotnet/tools"
dotnet --version                                 # → 10.0.x
```

Alternatif via apt (feed Microsoft, hanya kalau config distro-mu tersedia):

```bash
wget https://packages.microsoft.com/config/debian/13/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb
sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0
# untuk Ubuntu ganti "debian/13" → mis. "ubuntu/24.04"
```

> Jika `dotnet` mengeluh soal ICU/globalization: `export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.

**Sisanya:**

```bash
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

- Target MSBuild `BuildClientApp` otomatis menjalankan `npm ci && npm run build` ke `wwwroot/`,
  lalu menyalinnya ke `out/wwwroot/`. Lewati build SPA dengan `-p:BuildClient=false` bila
  sudah kamu build sendiri.

Verifikasi setelah publish — folder ini **harus ada**:

```bash
ls out/wwwroot/index.html          # kalau tidak ada → SPA tidak ikut ter-publish
```

> **Jika `/` memberi HTTP 404** dan log menampilkan `The WebRootPath was not found: .../out/wwwroot`,
> berarti `out/wwwroot` kosong. Perbaiki cepat: `cp -r wwwroot out/` lalu jalankan ulang,
> atau bangun SPA lebih dulu sebagai langkah terpisah:
> ```bash
> (cd ClientApp && npm ci && npm run build)      # menulis ../wwwroot
> dotnet publish -c Release -o out -p:BuildClient=false
> ```

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

## 4A. Jalankan sebagai service (systemd)

Agar tetap hidup setelah SSH ditutup, otomatis mulai saat boot, dan restart bila crash.
Asumsi: sudah di-publish ke `/srv/beelearn/BeeLearn/out`, dan .NET dipasang di
`~/.dotnet` (lewat `dotnet-install.sh`) untuk user `harvianto`.

**1) Siapkan folder data (di luar folder `out` supaya aman saat re-publish):**

```bash
sudo mkdir -p /srv/beelearn/data /srv/beelearn/.judge
sudo chown -R harvianto:harvianto /srv/beelearn
```

**2) Buat unit file:**

```bash
sudo tee /etc/systemd/system/beelearn.service > /dev/null <<'EOF'
[Unit]
Description=BeeLearn (Padlet + C/C++ online judge)
After=network-online.target
Wants=network-online.target

[Service]
Type=notify            # butuh build yang sudah memakai UseSystemd(); kalau belum, ganti: Type=simple
User=harvianto
Group=harvianto
WorkingDirectory=/srv/beelearn/BeeLearn/out
ExecStart=/srv/beelearn/BeeLearn/out/BeeLearn

Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:8080
Environment=DOTNET_ROOT=/home/harvianto/.dotnet
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1
# nilai mengandung spasi -> WAJIB dikutip penuh, kalau tidak systemd memecahnya
Environment="ConnectionStrings__Default=Data Source=/srv/beelearn/data/beelearn.db"
Environment=Judge__WorkRoot=/srv/beelearn/.judge

Restart=on-failure
RestartSec=5
TimeoutStartSec=60

[Install]
WantedBy=multi-user.target
EOF
```

> `DOTNET_ROOT` wajib karena .NET dipasang di home, bukan sistem. Kalau kamu memasang
> **ASP.NET Core Runtime** sistem-wide (`sudo .../dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/lib/dotnet`),
> baris `DOTNET_ROOT` bisa dihapus dan `User=` boleh diganti user khusus berprivilege rendah.

**3) Aktifkan & jalankan:**

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now beelearn
systemctl status beelearn --no-pager
journalctl -u beelearn -f            # ikuti log (Ctrl+C untuk keluar)
```

Buka `http://<ip-server>:8080`.

**Perintah harian:**

```bash
sudo systemctl restart beelearn
sudo systemctl stop beelearn
sudo systemctl disable --now beelearn      # matikan permanen
journalctl -u beelearn --since "10 min ago"
```

**Update ke versi baru:**

```bash
cd /srv/beelearn && git pull
cd BeeLearn && ~/.dotnet/dotnet publish -c Release -o out
sudo systemctl restart beelearn
```

**Catatan:**

- DB pindah ke `/srv/beelearn/data/beelearn.db` (path absolut). Saat pertama start via
  service, DB baru dibuat & di-seed ulang. Kalau mau bawa data lama:
  `mv /srv/beelearn/BeeLearn/out/beelearn.db* /srv/beelearn/data/` sebelum `enable`.
- `g++` ada di `/usr/bin` sehingga terjangkau PATH default systemd — tidak perlu setting tambahan.
- Judge menjalankan kode C++ murid sebagai user service. Untuk produksi sungguhan,
  pakai user khusus + pertimbangkan isolasi lebih kuat (lihat *Security note* di `README.md`).

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
| Publish → `/` HTTP 404, log: `WebRootPath was not found: .../out/wwwroot` | `out/wwwroot` kosong. `cp -r wwwroot out/` lalu jalankan ulang, atau build SPA lebih dulu lalu `dotnet publish ... -p:BuildClient=false`. |
| SignalR tidak connect di belakang proxy | Teruskan header `Upgrade`/`Connection` untuk WebSocket; pastikan path `/hubs` ikut ter-proxy. |
| Verdict selalu `MemoryLimit` untuk program sederhana | Naikkan `MemoryLimitKb` pada soal (mis. 65536) — `RLIMIT_AS` membatasi *virtual address space*, bukan RSS. |
| Gambar soal terlindungi kosong / error | Pada image minimal, `sudo apt-get install -y libfontconfig1`. Di Ubuntu 24.04 biasanya sudah cukup. |
| `Address already in use` | Ubah `applicationUrl` di `launchSettings.json` atau set `ASPNETCORE_URLS`, dan sesuaikan `BACKEND_URL` untuk Vite. |
| Service: `Format of the initialization string does not conform...` | Baris `Environment=` dengan nilai berspasi tidak dikutip. Bungkus penuh: `Environment="ConnectionStrings__Default=Data Source=/path/db"`. |
| Judge lambat / antre | Naikkan `Judge__MaxConcurrent` sesuai jumlah core. |
