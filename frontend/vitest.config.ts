import { fileURLToPath, URL } from 'node:url'

import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],

    env: {
      /*
       * Testerna körs medvetet i en annan tidszon än den svenska (§KM.5).
       *
       * Appen ska alltid visa Europe/Stockholm, aldrig webbläsarens egen zon — en förälder
       * på semester i Spanien ska se samma avsparkstid som en förälder hemma i Kärra.
       * Kör man testerna i svensk tid går ett fel av typen "glömde ange tidszon" igenom
       * obemärkt, eftersom rätt svar råkar bli detsamma.
       *
       * Los Angeles ligger nio timmar bort och byter sommartid vid andra datum, så både
       * klockslag och datumgränser skiljer sig.
       */
      TZ: 'America/Los_Angeles',
    },
  },
})
