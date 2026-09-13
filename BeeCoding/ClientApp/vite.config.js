import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import tailwindcss from '@tailwindcss/vite';

// Backend Kestrel port for dev proxy.
const backend = process.env.BACKEND_URL || 'http://localhost:5048';

// Set when the app is reverse-proxied under a subpath (e.g. VITE_BASE_PATH=/beecoding/
// so it lives at https://host/beecoding/ alongside other apps on the same domain).
// Must end with a slash. The matching backend setting is PathBase (see Program.cs) —
// keep the two in sync. Unset (root deploy) needs no changes anywhere.
const basePath = process.env.VITE_BASE_PATH || '/';

export default defineConfig({
  base: basePath,
  plugins: [vue(), tailwindcss()],
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    host: '0.0.0.0',
    proxy: {
      '/api': { target: backend, changeOrigin: true },
      '/hubs': { target: backend, changeOrigin: true, ws: true },
      '/lsp': { target: backend, changeOrigin: true, ws: true },
    },
  },
});
