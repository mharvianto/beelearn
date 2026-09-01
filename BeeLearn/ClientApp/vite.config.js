import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import tailwindcss from '@tailwindcss/vite';

// Backend Kestrel port for dev proxy.
const backend = process.env.BACKEND_URL || 'http://localhost:5048';

export default defineConfig({
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
    },
  },
});
