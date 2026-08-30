/**
 * Kontrastberäkning enligt WCAG 2.1.
 *
 * Låg härifrån i stället för i ett test, eftersom appen behöver samma matematik i drift:
 * lagfärgen kommer från databasen och är inte känd när stilmallen skrivs, så textfärgen
 * ovanpå den måste räknas fram (se `teamTheme.ts`).
 *
 * Att testerna använder exakt samma funktion som appen är själva poängen — annars mäter
 * testet en kopia och inte det som faktiskt visas för någon.
 */

/** Minsta kontrast för text, WCAG 2.1 AA (1.4.3). */
export const MIN_TEXT_CONTRAST = 4.5

/** Minsta kontrast för ramar och komponenter, WCAG 2.1 AA (1.4.11). */
export const MIN_UI_CONTRAST = 3

function channel(value: number): number {
  const c = value / 255

  return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
}

/** Relativ luminans, 0 för svart och 1 för vitt. */
export function relativeLuminance(hex: string): number {
  const h = hex.replace('#', '')

  if (!/^[0-9a-fA-F]{6}$/.test(h)) {
    throw new Error(`Förväntade en sexsiffrig hexfärg, fick "${hex}"`)
  }

  const [r, g, b] = [0, 2, 4].map((i) => channel(parseInt(h.slice(i, i + 2), 16)))

  return 0.2126 * (r ?? 0) + 0.7152 * (g ?? 0) + 0.0722 * (b ?? 0)
}

/** Kontrastkvot mellan två färger, mellan 1 och 21. Ordningen spelar ingen roll. */
export function contrastRatio(a: string, b: string): number {
  const [high, low] = [relativeLuminance(a), relativeLuminance(b)].sort((x, y) => y - x)

  return ((high ?? 0) + 0.05) / ((low ?? 0) + 0.05)
}
