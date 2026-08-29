import { fileURLToPath, URL } from 'node:url'

import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      // Speglar "paths" i tsconfig.app.json. Bada maste andras tillsammans.
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
})
