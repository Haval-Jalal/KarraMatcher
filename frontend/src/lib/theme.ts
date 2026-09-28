/**
 * Tema-val: System, Ljust eller Mörkt (`#redesign`).
 *
 * <h3>System är standard</h3>
 *
 * Utan ett eget val följer appen enhetens `prefers-color-scheme` — mor- och farföräldrar som
 * ställt hela telefonen mörk ska få appen mörk utan att röra något. Ett eget val skrivs som
 * attributet `data-theme` på `<html>` och vinner då över systemet (se stilmallen).
 *
 * <h3>Bara på den här enheten</h3>
 *
 * Valet är en bekvämlighet per enhet och bor i `localStorage` — det når aldrig servern. Läsning
 * och skrivning kan kasta (privat läge, blockerad lagring), så allt är try/catch:at och faller
 * tillbaka på System.
 *
 * Ett litet inline-skript i `index.html` sätter attributet innan appen laddas, så sidan aldrig
 * blinkar till i fel läge först.
 */

export type ThemePreference = 'system' | 'light' | 'dark'

const STORAGE_KEY = 'karra.theme'

/** Vad som ligger sparat, eller `system` när inget (eller något trasigt) finns. */
export function getStoredTheme(): ThemePreference {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored === 'light' || stored === 'dark' || stored === 'system') {
      return stored
    }
  } catch {
    // Lagringen kan vara blockerad (privat läge). Faller tillbaka på System.
  }

  return 'system'
}

/** Speglar valet till `<html data-theme>`; System tar bort attributet så media-frågan gäller. */
export function applyTheme(preference: ThemePreference): void {
  const root = document.documentElement

  if (preference === 'system') {
    root.removeAttribute('data-theme')
  } else {
    root.setAttribute('data-theme', preference)
  }
}

/** Sparar valet på enheten och tillämpar det direkt. */
export function setTheme(preference: ThemePreference): void {
  try {
    localStorage.setItem(STORAGE_KEY, preference)
  } catch {
    // Kan inte spara (privat läge) — tillämpa ändå för den här sessionen.
  }

  applyTheme(preference)
}
