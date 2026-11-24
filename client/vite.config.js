import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// In sviluppo (npm run dev) le chiamate /api vanno al sito pubblicato,
// oppure al backend locale se imposti API_URL (es. API_URL=http://localhost:5000 npm run dev).
export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': { target: process.env.API_URL || 'https://fantaaoe.tail17199a.ts.net', changeOrigin: true },
    },
  },
})
