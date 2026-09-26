import { fileURLToPath, URL } from 'node:url'

import basicSsl from '@vitejs/plugin-basic-ssl'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

/*
 * HTTPS bara nar E2E begar det (E2E_HTTPS=1). Da lagras refresh-cookien (Secure) aven pa
 * dev-servern, sa att en sida kan laddas om utan att tappa sin inloggning -- det som gor
 * fleranvandar-flodet (samakning) provbart. Vanlig `npm run dev` paverkas inte.
 */
const useHttps = process.env['E2E_HTTPS'] === '1'

// https://vite.dev/config/
/*
 * Backendens adress under lokal utveckling. Bara dev-servern ser den — den bundlas aldrig
 * in i klienten, till skillnad fran allt med VITE_-prefix.
 */
const apiTarget = process.env['KARRA_API_PROXY'] ?? 'http://localhost:5066'

export default defineConfig({
  plugins: [react(), ...(useHttps ? [basicSsl()] : [])],
  build: {
    /*
     * Töm dist/ före varje bygge. Det är Vites standard när outDir ligger i roten, men vi
     * sätter det uttryckligen: prestandabudget-grinden (scripts/check-bundle-size.mjs) summerar
     * varje fil i dist/assets, och en kvarlämnad chunk från ett tidigare bygge skulle blåsa upp
     * summan och ge ett falskt rött.
     */
    emptyOutDir: true,
  },
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
