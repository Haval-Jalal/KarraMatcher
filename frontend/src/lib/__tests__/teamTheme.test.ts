import { describe, expect, it } from 'vitest'

import { MIN_TEXT_CONTRAST, contrastRatio } from '@/lib/contrast'
import { inkFor, teamThemeStyle } from '@/lib/teamTheme'

/**
 * Lagfärgen som yta (`#117`).
 *
 * Lagen är fyra och färgerna spretar: Gul och Vit är ljusa, Blå och Svart är mörka. En
 * fast textfärg ovanpå dem kan inte fungera för alla fyra, och det var därför lagfärgen
 * tidigare bara fick vara en kantlinje.
 *
 * Testet vaktar att den härledda textfärgen håller för **alla fyra lagen** — inte bara
 * de två som råkar ligga i testdatan — och för färger vi inte känner till än. Nästa lag
 * läggs in av en tränare i M3, utan att någon rör stilmallen.
 */

/** Speglar `SeedData.cs`. Fyra lag, fyra ljushetsnivåer. */
const TEAM_COLOURS: [string, string][] = [
  ['Gul', '#D9A21B'],
  ['Blå', '#1E3F8A'],
  ['Vit', '#D9D9D9'],
  ['Svart', '#161616'],
]

describe('bläcket håller mot varje lagfärg', () => {
  it.each(TEAM_COLOURS)('%s (%s) når AA-kontrast', (_name, colour) => {
    expect(contrastRatio(inkFor(colour), colour)).toBeGreaterThanOrEqual(MIN_TEXT_CONTRAST)
  })

  it('ger de ljusa och de mörka lagen olika bläck', () => {
    // Går den här sönder har någon råkat låsa textfärgen igen, och då är det ett av
    // lagen som blivit oläsbart — förmodligen Vit eller Svart, som ligger ytterst.
    expect(inkFor('#D9D9D9')).not.toBe(inkFor('#161616'))
  })

  it('väljer den av de två som ger mest marginal', () => {
    // Inte den första som råkar duga. Marginalen är det som gör skärmen läsbar i solsken.
    expect(inkFor('#D9A21B')).toBe('#000000')
    expect(inkFor('#1E3F8A')).toBe('#ffffff')
  })
})

describe('också för lagfärger som inte finns än', () => {
  it('håller AA för hela färgrymden i grova steg', () => {
    // En tränare lägger in ett nytt lag i M3 och skriver in en hexfärg. Ingen kommer att
    // räkna kontrast åt hen, så funktionen måste hålla för vad som helst.
    const steps = [0, 51, 102, 153, 204, 255]
    const failures: string[] = []

    for (const r of steps) {
      for (const g of steps) {
        for (const b of steps) {
          const hex = '#' + [r, g, b].map((v) => v.toString(16).padStart(2, '0')).join('')

          if (contrastRatio(inkFor(hex), hex) < MIN_TEXT_CONTRAST) {
            failures.push(hex)
          }
        }
      }
    }

    expect(failures).toEqual([])
  })

  it('avvisar något som inte är en hexfärg', () => {
    // Fältet kommer från databasen. Ett tomt eller trasigt värde ska säga ifrån, inte
    // tyst ge en osynlig text.
    expect(() => inkFor('rebeccapurple')).toThrow(/hexfärg/)
  })
})

describe('temavariablerna', () => {
  it('sätter både färgen och bläcket', () => {
    expect(teamThemeStyle('#1E3F8A')).toEqual({
      '--team-accent': '#1E3F8A',
      '--team-ink': '#ffffff',
    })
  })

  it('lämnar temat orört innan laget är hämtat', () => {
    // Startsidan och den korta stunden innan lagen laddats. Gråtonen i :root gäller då.
    expect(teamThemeStyle(undefined)).toBeUndefined()
  })
})
