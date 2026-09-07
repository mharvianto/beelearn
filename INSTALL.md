# BeeCoding — Panduan Instalasi

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
| **clangd** | opsional (14+, diuji 18) | IntelliSense C/C++ di editor (autocomplete, hover, diagnostik). Non-aktif secara default (`Lsp:Enabled=false`); tanpa clangd editor jatuh ke completion berbasis kata. |

Yang **tidak perlu** dipasang:

- Font — DejaVu TTF sudah disertakan di `BeeCoding/Assets/fonts/` (dipakai untuk render soal-terenkripsi).
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

# opsional: IntelliSense C/C++ di editor (lihat §5 untuk mengaktifkan)
sudo apt-get install -y clangd
```

---

## 2. Ambil kode

```bash
git clone <URL-repo> beecoding
cd beecoding
```

---

## 3. Menjalankan untuk pengembangan (dua terminal)

### Terminal 1 — backend

```bash
cd BeeCoding
dotnet run
```

- Mendengarkan di **http://localhost:5048** (profil `http` di `Properties/launchSettings.json`).
- Saat pertama kali: `beecoding.db` dibuat, migrasi dijalankan, lalu data demo + **bank 81 soal** di-seed otomatis.
- Startup akan mencetak mode sandbox, mis.
  `Sandbox mode: rlimits only (bwrap ...)` atau `bubblewrap + rlimits`.

### Terminal 2 — frontend (Vite dev server)

```bash
cd BeeCoding/ClientApp
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
cd BeeCoding
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
./BeeCoding
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
Asumsi: sudah di-publish ke `/srv/beecoding/BeeCoding/out`, dan .NET dipasang di
`~/.dotnet` (lewat `dotnet-install.sh`) untuk user `harvianto`.

**1) Siapkan folder data (di luar folder `out` supaya aman saat re-publish):**

```bash
sudo mkdir -p /srv/beecoding/data /srv/beecoding/.judge
sudo chown -R harvianto:harvianto /srv/beecoding
```

**2) Buat unit file:**

```bash
sudo tee /etc/systemd/system/beecoding.service > /dev/null <<'EOF'
[Unit]
Description=BeeCoding (Padlet + C/C++ online judge)
After=network-online.target
Wants=network-online.target

[Service]
Type=notify            # butuh build yang sudah memakai UseSystemd(); kalau belum, ganti: Type=simple
User=harvianto
Group=harvianto
WorkingDirectory=/srv/beecoding/BeeCoding/out
ExecStart=/srv/beecoding/BeeCoding/out/BeeCoding

Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:8080
Environment=DOTNET_ROOT=/home/harvianto/.dotnet
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1
# nilai mengandung spasi -> WAJIB dikutip penuh, kalau tidak systemd memecahnya
Environment="ConnectionStrings__Default=Data Source=/srv/beecoding/data/beecoding.db"
Environment=Judge__WorkRoot=/srv/beecoding/.judge

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
sudo systemctl enable --now beecoding
systemctl status beecoding --no-pager
journalctl -u beecoding -f            # ikuti log (Ctrl+C untuk keluar)
```

Buka `http://<ip-server>:8080`.

**Perintah harian:**

```bash
sudo systemctl restart beecoding
sudo systemctl stop beecoding
sudo systemctl disable --now beecoding      # matikan permanen
journalctl -u beecoding --since "10 min ago"
```

**Update ke versi baru:**

```bash
cd /srv/beecoding && git pull
cd BeeCoding && ~/.dotnet/dotnet publish -c Release -o out
sudo systemctl restart beecoding
```

**Catatan:**

- DB pindah ke `/srv/beecoding/data/beecoding.db` (path absolut). Saat pertama start via
  service, DB baru dibuat & di-seed ulang. Kalau mau bawa data lama:
  `mv /srv/beecoding/BeeCoding/out/beecoding.db* /srv/beecoding/data/` sebelum `enable`.
- `g++` ada di `/usr/bin` sehingga terjangkau PATH default systemd — tidak perlu setting tambahan.
- Judge menjalankan kode C++ murid sebagai user service. Untuk produksi sungguhan,
  pakai user khusus + pertimbangkan isolasi lebih kuat (lihat *Security note* di `README.md`).

---

## 4B. nginx (reverse proxy) + SSL/HTTPS

BeeCoding dijalankan di `127.0.0.1:8080`, nginx di depan menangani TLS + WebSocket.

### 1) Kunci app hanya ke localhost

Di `/etc/systemd/system/beecoding.service` ubah:

```
Environment=ASPNETCORE_URLS=http://127.0.0.1:8080
```

lalu `sudo systemctl daemon-reload && sudo systemctl restart beecoding`.
(Dukungan `X-Forwarded-Proto` sudah ada di aplikasi — `UseForwardedHeaders`, jadi cookie
dan `Request.Scheme` mengikuti HTTPS.)

### 2) Pasang nginx

```bash
sudo apt-get install -y nginx
```

### 3) Config situs — mulai **HTTP-only**

Blok `map`/`upstream`/`proxy` di bawah dipakai oleh kedua opsi TLS. Jangan tulis blok
`listen 443` dulu — certbot yang menambahkannya (Opsi A), atau kamu tambah manual setelah
sertifikat dibuat (Opsi B).

```bash
sudo tee /etc/nginx/sites-available/beecoding > /dev/null <<'EOF'
# WebSocket upgrade (dipakai SignalR di /hubs)
map $http_upgrade $connection_upgrade { default upgrade; '' close; }

upstream beecoding { server 127.0.0.1:8080; keepalive 32; }

server {
    listen 80;
    listen [::]:80;
    server_name beecoding.example.com;     # <-- DOMAIN ASLI (Opsi A) atau IP LAN (Opsi B)

    client_max_body_size 4m;              # kiriman kode murid (<=200 KB) + margin

    location / {
        proxy_pass http://beecoding;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade           $http_upgrade;
        proxy_set_header Connection        $connection_upgrade;
    }

    # SignalR (/hubs) + clangd LSP (/lsp): koneksi persisten -> timeout panjang
    location ~ ^/(hubs|lsp)/ {
        proxy_pass http://beecoding;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade           $http_upgrade;
        proxy_set_header Connection        $connection_upgrade;
        proxy_read_timeout  3600s;
        proxy_send_timeout  3600s;
        proxy_buffering     off;
    }
}
EOF

sudo ln -sf /etc/nginx/sites-available/beecoding /etc/nginx/sites-enabled/beecoding
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx
```

### 4) Sertifikat — pilih salah satu

**Opsi A — Let's Encrypt** — hanya jika ada **domain publik** yang resolve ke **IP publik**
server ini dan **port 80 terbuka dari internet**:

```bash
sudo apt-get install -y certbot python3-certbot-nginx
sudo certbot --nginx -d beecoding.example.com
```

certbot mengubah `server` HTTP tadi menjadi HTTPS (menambah `listen 443 ssl`, path
sertifikat `/etc/letsencrypt/live/...`, dan redirect 80→443), lalu memasang timer
perpanjangan (`systemctl list-timers 'certbot*'`). Selesai.

> Kalau server hanya di LAN / tanpa domain publik, certbot **tidak akan berhasil**
> (validasi HTTP-01 gagal). Pakai Opsi B.

**Opsi B — self-signed (LAN / tanpa domain):**

```bash
# 1) buat sertifikat DULU
sudo openssl req -x509 -nodes -newkey rsa:2048 -days 825 \
  -keyout /etc/ssl/private/beecoding.key \
  -out /etc/ssl/certs/beecoding.crt \
  -subj "/CN=192.168.50.7" \
  -addext "subjectAltName=IP:192.168.50.7"        # samakan dengan alamat yang dipakai klien
```

```bash
# 2) baru ganti config: 80 -> redirect, tambah server 443
sudo tee /etc/nginx/sites-available/beecoding > /dev/null <<'EOF'
map $http_upgrade $connection_upgrade { default upgrade; '' close; }
upstream beecoding { server 127.0.0.1:8080; keepalive 32; }

server {
    listen 80;
    listen [::]:80;
    server_name 192.168.50.7;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    listen [::]:443 ssl;
    http2 on;
    server_name 192.168.50.7;

    ssl_certificate     /etc/ssl/certs/beecoding.crt;
    ssl_certificate_key /etc/ssl/private/beecoding.key;
    ssl_protocols       TLSv1.2 TLSv1.3;

    client_max_body_size 4m;

    location / {
        proxy_pass http://beecoding;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Real-IP         $remote_addr;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade           $http_upgrade;
        proxy_set_header Connection        $connection_upgrade;
    }
    # SignalR (/hubs) + clangd LSP (/lsp)
    location ~ ^/(hubs|lsp)/ {
        proxy_pass http://beecoding;
        proxy_http_version 1.1;
        proxy_set_header Host              $host;
        proxy_set_header X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header Upgrade           $http_upgrade;
        proxy_set_header Connection        $connection_upgrade;
        proxy_read_timeout  3600s;
        proxy_send_timeout  3600s;
        proxy_buffering     off;
    }
}
EOF
sudo nginx -t && sudo systemctl reload nginx
```

Browser akan menandai "not trusted" (wajar untuk sertifikat sendiri) — lanjutkan saja,
atau impor `beecoding.crt` ke *trust store* perangkat klien. Alternatif yang otomatis
dipercaya di jaringan lokal: pakai **mkcert**.

**Opsi C — di belakang CGNAT / ISP blokir port 80-443 (Cloudflare Tunnel):**

Sertifikat valid tanpa membuka port apa pun. Butuh **domain sendiri** yang dikelola
Cloudflare (`*.synology.me` / DDNS gratis tidak bisa).

```bash
curl -L https://pkg.cloudflare.com/cloudflare-main.gpg | sudo tee /usr/share/keyrings/cloudflare-main.gpg >/dev/null
echo "deb [signed-by=/usr/share/keyrings/cloudflare-main.gpg] https://pkg.cloudflare.com/cloudflared any main" | sudo tee /etc/apt/sources.list.d/cloudflared.list
sudo apt-get update && sudo apt-get install -y cloudflared

cloudflared tunnel login
cloudflared tunnel create beecoding
cloudflared tunnel route dns beecoding beecoding.domainmu.com

mkdir -p ~/.cloudflared && cat > ~/.cloudflared/config.yml <<EOF
tunnel: beecoding
credentials-file: $HOME/.cloudflared/<TUNNEL-ID>.json
ingress:
  - hostname: beecoding.domainmu.com
    service: http://127.0.0.1:8080
  - service: http_status:404
EOF

sudo cloudflared service install
sudo systemctl enable --now cloudflared
```

nginx tidak wajib di jalur ini (tunnel langsung ke `127.0.0.1:8080`); WebSocket SignalR
didukung Cloudflare. TLS ditangani di edge Cloudflare.

### 5) Firewall & uji

```bash
sudo apt-get install -y ufw && sudo ufw allow 'Nginx Full' && sudo ufw allow OpenSSH && sudo ufw enable
```

Buka `https://<domain-atau-IP>/`. Uji API: `curl -k https://<host>/api/auth/me` → `401`.

### Catatan

| Masalah | Solusi |
|---|---|
| SignalR putus-nyambung / "WebSocket closed" | Pastikan blok `map $http_upgrade` ada dan header `Upgrade`/`Connection` diteruskan. |
| Login berhasil tapi langsung ter-logout | app harus di belakang HTTPS **dan** menerima `X-Forwarded-Proto` (sudah default). Jangan campur akses `http://` dan `https://`. |
| 413 Request Entity Too Large saat submit | naikkan `client_max_body_size`. |
| 502 Bad Gateway | `beecoding.service` mati / bukan di `127.0.0.1:8080`. Cek `systemctl status beecoding`. |
| certbot: `cannot load certificate ".../beecoding.crt"` saat `nginx -t` | Config sudah punya blok `listen 443 ssl` menunjuk file yang belum ada. Mulai dari config **HTTP-only** (langkah 3), baru jalankan `certbot --nginx`. |
| certbot: `Timeout during connect (likely firewall problem)` | DNS benar, tapi port 80 dari internet tidak sampai ke server (ISP blokir / NAT ganda / CGNAT). Buka port 80+443 di router, atau pakai **Opsi C (Cloudflare Tunnel)**, atau **Opsi B (self-signed)** untuk LAN. |
| certbot: challenge gagal / `NXDOMAIN` | Domain tidak resolve ke IP publik server ini. Perbaiki DNS/DDNS, atau Opsi B/C. |
| certbot: `Could not automatically find a matching server block` | `server_name` di config nginx masih placeholder. Set `server_name <domain-asli>;`, `reload`, lalu `sudo certbot install --cert-name <domain>`. |

---

## 5. Konfigurasi

Ubah lewat `BeeCoding/appsettings.json`, `appsettings.Production.json`, atau environment
variable (nesting pakai `__`).

| Kunci | Default | Keterangan |
|---|---|---|
| `ConnectionStrings:Default` | `Data Source=beecoding.db` | Path file SQLite. Produksi: `Data Source=/var/lib/beecoding/beecoding.db`. |
| `Judge:WorkRoot` | `/tmp/beecoding-judge` | Direktori kerja compile/run. |
| `Judge:MaxConcurrent` | `2` | Jumlah compile/run paralel. |
| `Judge:CompileTimeoutMs` | `10000` | Batas waktu kompilasi. |
| `Judge:QueueCapacity` | `200` | Kapasitas antrean judge. |
| `Judge:HardWallBufferMs` | `800` | Selisih wall-clock di atas batas soal sebelum di-*kill* paksa. |
| `Judge:RunTimeLimitMs` | `1000` | Batas waktu default tombol **Run** ad-hoc. |
| `Judge:RunMemoryLimitKb` | `32768` | Batas memori default tombol **Run**. |
| `Judge:RateLimitMs` | `1500` | Jarak minimum antar Run/Submit per user. |
| `Lsp:Enabled` | `false` | Aktifkan IntelliSense C/C++ (butuh `clangd` di PATH). |
| `Lsp:ClangdPath` | `clangd` | Path biner clangd. |
| `Lsp:MaxConcurrent` | `4` | Maksimum sesi clangd bersamaan (1 per editor yang terbuka). |
| `Lsp:IdleTimeoutSeconds` | `300` | Sesi clangd dimatikan setelah sekian detik tanpa lalu-lintas. |
| `Lsp:MemoryLimitMb` | `0` | *Runaway guard* opsional (RLIMIT_DATA via `prlimit`). `0` = nonaktif. Isi longgar (≥2048); nilai terlalu kecil membuat clangd *abort* saat meng-index header standar. |
| `Admin:Token` | `""` (kosong) | Token untuk endpoint `/api/admin/*` (mengisi bank soal via skrip). Kosong ⇒ endpoint mati (404). |
| `ASPNETCORE_URLS` | — | mis. `http://0.0.0.0:8080`. |

Contoh override via env:

```bash
export ConnectionStrings__Default="Data Source=/var/lib/beecoding/beecoding.db"
export Judge__MaxConcurrent=4
export Judge__WorkRoot=/var/tmp/beecoding-judge
export Lsp__Enabled=true          # setelah `apt install clangd`
export Admin__Token="$(openssl rand -hex 24)"   # aktifkan endpoint admin bank soal
```

### Endpoint admin — mengisi bank soal via skrip

Aktif hanya kalau `Admin:Token` di-set. Kirim token sebagai header `X-Admin-Token`
(atau query `?token=`). Semua di bawah `/api/admin` — **bukan** cookie login.

| Method & path | Fungsi |
|---|---|
| `GET /api/admin/ping` | cek token; balas owner default + jumlah soal bank |
| `GET /api/admin/bank-problems` | daftar semua soal bank (semua owner) |
| `POST /api/admin/bank-problems` | buat/update batch soal (upsert per *(owner, title)*) |
| `DELETE /api/admin/bank-problems/{id}` | hapus satu soal |

Soal yang dibuat masuk ke **bank publik** (muncul di **Practice**, dan guru bisa
meng-copy-nya ke board lewat `POST /api/bank/{id}/copy-to/{slug}`). Body `POST`:

```jsonc
{
  "ownerEmail": "teacher@demo.test",   // opsional; default = guru pertama
  "replaceExisting": true,             // opsional; true = timpa yang judul-nya sama
  "problems": [
    {
      "title": "Sum of Array",
      "statementMarkdown": "Baca n lalu n bilangan, cetak jumlahnya.",
      "language": "cpp",               // "c" | "cpp" (default cpp)
      "level": "Easy",                 // Easy | Medium | Hard (default Medium)
      "tags": "array, math",
      "starterCode": "#include <bits/stdc++.h>\n...",
      "timeLimitMs": 1000,             // opsional
      "memoryLimitKb": 32768,          // opsional
      "isPublic": true,                // opsional (default true)
      "tests": [
        { "stdin": "3\n1 2 3\n", "expectedStdout": "6\n", "isSample": true },
        { "stdin": "1\n-5\n",    "expectedStdout": "-5\n" }
      ]
    }
  ]
}
```

Aturan validasi per soal: `title` wajib, minimal 1 test, dan minimal 1 test **penilaian**
(`isSample:false`, `points`>0). Soal yang gagal dilewati dan dilaporkan di `errors`; sisanya
tetap tersimpan. Contoh:

```bash
curl -sS -X POST http://localhost:5048/api/admin/bank-problems \
  -H "X-Admin-Token: $Admin__Token" -H 'Content-Type: application/json' \
  -d @soal.json
# -> {"created":[...],"updated":[...],"errors":[...]}
```

**IntelliSense C/C++ (clangd).** Bila `Lsp:Enabled=true` dan `clangd` ada di PATH, editor
Monaco di halaman **Solve** dan **Practice** memakai clangd sebagai *language server* lewat
WebSocket `/lsp/cpp`: autocomplete, hover, *signature help*, dan diagnostik sebaris. Setiap
editor yang terbuka memakai satu proses clangd berumur pendek dengan *workspace* satu file
sementara; proses dimatikan saat editor ditutup atau `Lsp:IdleTimeoutSeconds` terlewati.
Di belakang nginx, blok `location ~ ^/(hubs|lsp)/` di §4B sudah menangani *upgrade* WebSocket-nya.
Tanpa clangd atau dengan `Lsp:Enabled=false`, editor tetap jalan memakai completion berbasis kata.

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

cd BeeCoding
dotnet ef migrations add NamaMigrasi
dotnet ef database update
```

**Reset data pengembangan:** hentikan aplikasi, `rm BeeCoding/beecoding.db*`, jalankan lagi →
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
