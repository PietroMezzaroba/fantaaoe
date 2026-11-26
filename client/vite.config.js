import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

// In sviluppo (npm run dev) le chiamate /api vanno al backend locale (http://localhost:59173, profilo di FantaAoe.Api).
// Per puntare altrove imposta API_URL (es. API_URL=https://fantaaoe.tail17199a.ts.net npm run dev),
// oppure scrivilo in client/.env.local (API_URL=https://fantaaoe.tail17199a.ts.net), che git ignora.
export default defineConfig(({ mode }) => {
  const env = { ...loadEnv(mode, process.cwd(), ''), ...process.env }
  const api = env.API_URL || 'http://localhost:59173'
  return {
    plugins: [vue()],
    server: {
      proxy: {
        '/hubs': { target: api, changeOrigin: true, ws: true }, // SignalR (asta in tempo reale)
        '/api': { target: api, changeOrigin: true },
      },
    },
  }
})
