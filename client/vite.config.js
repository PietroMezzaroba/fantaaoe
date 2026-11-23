import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// In sviluppo (npm run dev) le chiamate /api vanno al sito pubblicato.
export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': { target: 'https://fantaaoe.tail17199a.ts.net', changeOrigin: true },
    },
  },
})
