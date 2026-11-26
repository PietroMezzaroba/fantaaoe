import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// In sviluppo (npm run dev) le chiamate /api vanno al backend locale (http://localhost:59173, profilo di FantaAoe.Api).
// Per puntare altrove imposta API_URL (es. API_URL=https://fantaaoe.tail17199a.ts.net npm run dev).
export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/hubs': { target: process.env.API_URL || 'http://localhost:59173', changeOrigin: true, ws: true }, // SignalR (asta in tempo reale)
      '/api': { target: process.env.API_URL || 'http://localhost:59173', changeOrigin: true },
    },
  },
})
