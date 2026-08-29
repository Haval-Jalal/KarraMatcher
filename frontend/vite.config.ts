import { fileURLToPath, URL } from 'node:url'

import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
/*
 * Backendens adress under lokal utveckling. Bara dev-servern ser den — den bundlas aldrig
 * in i klienten, till skillnad fran allt med VITE_-prefix.
 */
const apiTarget = process.env['KARRA_API_PROXY'] ?? 'http://localhost:5066'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      // Speglar "paths" i tsconfig.app.json. Bada maste andras tillsammans.
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    /*
     * Dev-servern proxar /api till backend, precis som Vercel gor i drift (KM.11).
     *
     * Poangen ar inte bekvamlighet utan att lokal utveckling ska ha *samma* form som
     * produktion: en enda origin. Utan proxyn skulle klienten behova anropa
     * http://localhost:5066 direkt, vilket kraver CORS -- och KM.11 sager uttryckligen
     * att CORS inte ska oppnas. Da hade vi haft en uppsattning lokalt och en annan i
     * drift, vilket ar precis sa CORS-fel uppstar dar de ar svarast att forsta.
     */
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
})
