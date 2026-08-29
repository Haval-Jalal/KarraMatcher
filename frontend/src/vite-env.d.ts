/// <reference types="vite/client" />

/**
 * Miljövariabler måste typas för att `import.meta.env` ska vara typsäkert.
 * Allt med `VITE_`-prefix bundlas in i klienten och är därmed publikt —
 * lägg aldrig en hemlighet här (CLAUDE.md → Frontend, Miljövariabler).
 */
interface ImportMetaEnv {
  /**
   * Bas-URL för API:t. Tom i drift, eftersom Vercel rewriter `/api/*` till Render och
   * klienten därmed ser en enda origin (§KM.11). Lokalt pekar den på backend direkt.
   */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
