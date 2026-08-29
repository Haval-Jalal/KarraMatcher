/**
 * Läs och skriv i webbläsarens lagring utan att appen går sönder när den inte finns.
 *
 * `localStorage` kastar i privat läge i vissa webbläsare, och kan vara helt avstängd av
 * en företagspolicy. Ett tappat lagval är en olägenhet; en vit skärm är ett fel.
 */

export function readSetting(key: string): string | null {
  try {
    return globalThis.localStorage?.getItem(key) ?? null
  } catch {
    return null
  }
}

export function writeSetting(key: string, value: string): void {
  try {
    globalThis.localStorage?.setItem(key, value)
  } catch {
    // Medvetet tyst. Att inte kunna spara valet får inte avbryta något.
  }
}
