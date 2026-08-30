import { describe, expect, it } from 'vitest'

import CSS_SOURCE from '@/styles/index.css?raw'

/**
 * Kontrasten mäts mot den stilmall som faktiskt levereras.
 *
 * WCAG 2.1 AA är ett eget krav i det här projektet (§KM.0 A3) — mor- och farföräldrar är
 * riktiga användare, och några av dem har nedsatt syn. Ett tal i en CSS-fil går att ändra
 * på en sekund, och ingen ser skillnaden förrän någon inte kan läsa appen.
 *
 * Värdena läses ur källan i stället för att skrivas av. En kopia hade kunnat gå grön medan
 * paletten drev iväg.
 */

const REQUIRED_TEXT = 4.5
const REQUIRED_UI = 3.0

/** Plockar ut ett tokenvärde ur ett block i stilmallen. */
function token(name: string, scope: 'light' | 'dark'): string {
  const source =
    scope === 'light'
      ? CSS_SOURCE.slice(0, CSS_SOURCE.indexOf('@media (prefers-color-scheme: dark)'))
      : CSS_SOURCE.slice(CSS_SOURCE.indexOf('@media (prefers-color-scheme: dark)'))

  const match = new RegExp(`--${name}:\\s*(#[0-9a-fA-F]{6})`).exec(source)

  if (!match?.[1]) {
    throw new Error(`Hittade inte --${name} i ${scope} läge`)
  }

  return match[1]
}

function channel(value: number): number {
  const c = value / 255

  return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
}

function luminance(hex: string): number {
  const h = hex.replace('#', '')
  const [r, g, b] = [0, 2, 4].map((i) => channel(parseInt(h.slice(i, i + 2), 16)))

  return 0.2126 * (r ?? 0) + 0.7152 * (g ?? 0) + 0.0722 * (b ?? 0)
}

export function contrast(a: string, b: string): number {
  const [high, low] = [luminance(a), luminance(b)].sort((x, y) => y - x)

  return ((high ?? 0) + 0.05) / ((low ?? 0) + 0.05)
}

const SCOPES = ['light', 'dark'] as const

describe('textkontrast är minst 4.5:1', () => {
  const pairs: [string, string][] = [
    ['text', 'surface'],
    ['text', 'surface-raised'],
    ['text-strong', 'surface'],
    ['text-strong', 'surface-raised'],
    ['text-muted', 'surface'],
    ['text-muted', 'surface-raised'],
    ['danger', 'surface'],
    ['danger', 'surface-raised'],
  ]

  for (const scope of SCOPES) {
    it.each(pairs)(`%s mot %s i ${scope} läge`, (foreground, background) => {
      const ratio = contrast(token(foreground, scope), token(background, scope))

      expect(ratio).toBeGreaterThanOrEqual(REQUIRED_TEXT)
    })
  }
})

describe('kontrast för komponenter är minst 3:1', () => {
  // WCAG 1.4.11. Gäller ramar som identifierar en komponent och den synliga
  // fokusmarkeringen — inte dekorativa avskiljare, där texten bär innehållet själv.
  const pairs: [string, string][] = [
    ['border-strong', 'surface'],
    ['border-strong', 'surface-raised'],
    ['focus', 'surface'],
    ['focus', 'surface-raised'],
  ]

  for (const scope of SCOPES) {
    it.each(pairs)(`%s mot %s i ${scope} läge`, (foreground, background) => {
      const ratio = contrast(token(foreground, scope), token(background, scope))

      expect(ratio).toBeGreaterThanOrEqual(REQUIRED_UI)
    })
  }
})

describe('stilmallen använder rätt ram på rätt ställe', () => {
  it('markerar aktuellt lag med något annat än lagfärgen', () => {
    // Gul ligger på 2,15:1 och Vit på 1,32:1 mot underlaget. Med lagfärgen som ram hade
    // markeringen varit osynlig för hälften av lagen.
    const block = CSS_SOURCE.slice(
      CSS_SOURCE.indexOf(".team-picker__option[aria-current='page']"),
      CSS_SOURCE.indexOf('.team-picker__swatch'),
    )

    expect(block).not.toContain('var(--team-color)')
  })

  it('ger lagbrickan en ring som själv når 3:1', () => {
    // Ringen är det som gör den vita och den svarta brickan synlig alls.
    const block = CSS_SOURCE.slice(
      CSS_SOURCE.indexOf('.team-picker__swatch {'),
      CSS_SOURCE.indexOf('.team-picker__name'),
    )

    expect(block).toContain('var(--border-strong)')
  })

  it('tar aldrig bort fokusmarkeringen', () => {
    // outline: none utan ersättning är det enskilt vanligaste tillgänglighetsfelet i en
    // webbapp, och det märks inte förrän någon försöker använda tangentbord.
    //
    // Kommentarerna rensas först: annars fastnar testet på texten "Aldrig outline: none"
    // i stilmallen, vilket det gjorde när det skrevs.
    const declarations = CSS_SOURCE.replace(/\/\*[\s\S]*?\*\//g, '')

    expect(declarations).not.toMatch(/outline:\s*(none|0)/)
  })

  it('respekterar prefers-reduced-motion', () => {
    expect(CSS_SOURCE).toContain('@media (prefers-reduced-motion: reduce)')
  })
})
