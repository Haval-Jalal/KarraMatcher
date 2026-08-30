import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { MatchList } from '@/features/matches'
import CSS_SOURCE from '@/styles/index.css?raw'
import { testMatch } from '@/test/apiStub'
import { renderWithRouter } from '@/test/renderWithRouter'

/**
 * Matchkortet som visuellt objekt (`#116`).
 *
 * Två saker låses här. Det ena är vad kortet *säger* — uppläsningsordningen är prövad
 * med VoiceOver och TalkBack i `#28`, och en omskrivning av utseendet får inte kasta om
 * den. Det andra är att de skillnader kortet visar aldrig bärs av färg ensam: både
 * hemma/borta och inställd match ska gå att uppfatta av någon som inte skiljer färger
 * (WCAG 1.4.1).
 *
 * Reglerna läses ur stilmallen i stället för att skrivas av, av samma skäl som i
 * kontrasttestet: en avskrift kan gå grön medan den riktiga filen glidit iväg.
 */

const now = '2026-09-15T09:00:00Z'

/**
 * Plockar ut en regel ur stilmallen, från selektorn till blockets slut.
 *
 * Selektorn måste börja på en egen rad. Utan den förankringen träffar `.match-card`
 * även `a.match-card` — länkåterställningen, som står tidigare i filen och inte säger
 * något om lådan.
 */
function rule(selector: string): string {
  const start = CSS_SOURCE.indexOf('\n' + selector + ' {')

  if (start === -1) {
    throw new Error(`Hittade ingen regel för ${selector}`)
  }

  return CSS_SOURCE.slice(start, CSS_SOURCE.indexOf('}', start))
}

describe('kortet läses upp i den ordning som prövats på riktiga telefoner', () => {
  it('ger länken tid, datum, motståndare och plats i den följden', async () => {
    await renderWithRouter(
      <MatchList
        matches={[
          testMatch('a', '2026-09-20T12:00:00Z', {
            opponent: 'Hisingsbacka FC',
            venue: {
              name: 'Klarebergsvallen',
              address: 'Klarebergsvallen, Karra',
              latitude: 57.8,
              longitude: 12,
            },
          }),
        ]}
        now={now}
      />,
    )

    const link = screen.getByRole('link', { name: /Hisingsbacka/ })

    // 12:00 UTC är 14:00 svensk sommartid. Ordningen är det testet vaktar.
    expect(link).toHaveAccessibleName(/14:00.*20 september.*Hisingsbacka FC.*Klarebergsvallen/s)
  })
})

describe('hemma och borta går att skilja åt utan att läsa', () => {
  it('märker korten olika', async () => {
    await renderWithRouter(
      <MatchList
        matches={[
          testMatch('hemma', '2026-09-20T12:00:00Z', { opponent: 'Hemmalaget', isHome: true }),
          testMatch('borta', '2026-09-27T12:00:00Z', { opponent: 'Bortalaget', isHome: false }),
        ]}
        now={now}
      />,
    )

    expect(screen.getByRole('link', { name: /Hemmalaget/ })).toHaveClass('match-card--home')
    expect(screen.getByRole('link', { name: /Bortalaget/ })).toHaveClass('match-card--away')
  })

  it('skiljer dem åt med form och inte bara med färg', () => {
    // Det här är hela poängen med kriteriet. Vore skillnaden enbart en färg vore kortet
    // oläsbart för den som inte skiljer dem — och lagfärgen duger dessutom inte som
    // bärare: Gul ligger på 2,15:1 mot underlaget.
    expect(rule('.match-card--home')).toContain('border-left-style: solid')
    expect(rule('.match-card--away')).toContain('border-left-style: dashed')
  })

  it('säger det i text också', async () => {
    await renderWithRouter(
      <MatchList matches={[testMatch('b', '2026-09-20T12:00:00Z', { isHome: false })]} now={now} />,
    )

    expect(screen.getByText(/Borta mot/)).toBeInTheDocument()
  })
})

describe('inställd match är omisskännlig', () => {
  it('märks med text före motståndaren', async () => {
    await renderWithRouter(
      <MatchList
        matches={[
          testMatch('a', '2026-09-20T12:00:00Z', { opponent: 'Torslanda', status: 'Cancelled' }),
        ]}
        now={now}
      />,
    )

    // Märkningen ska höras innan motståndaren, annars hinner man tro att matchen spelas.
    expect(screen.getByRole('link', { name: /Torslanda/ })).toHaveAccessibleName(
      /Inställd.*Torslanda/s,
    )
  })

  it('får en egen märkning på kortet', async () => {
    await renderWithRouter(
      <MatchList
        matches={[testMatch('a', '2026-09-20T12:00:00Z', { status: 'Cancelled' })]}
        now={now}
      />,
    )

    expect(screen.getByRole('link', { name: /Inställd/ })).toHaveClass('match-card--cancelled')
  })

  it('stryker över tiden — en form, inte en färg', () => {
    expect(rule('.match-card--cancelled .match-card__time')).toContain(
      'text-decoration: line-through',
    )
  })
})

describe('tiden bär kortet', () => {
  it('sätts i det smala snittet och i skalans näst största steg', () => {
    const time = rule('.match-card__time')

    expect(time).toContain('font-family: var(--display)')
    expect(time).toContain('font-size: var(--text-xl)')
  })

  it('är tyngre än motståndaren och platsen', () => {
    // Hierarkin är kriteriet. Blir de lika tunga har kortet slutat fungera på avstånd.
    expect(rule('.match-card__time')).toContain('font-weight: 700')
    expect(rule('.match-card__venue')).toContain('font-size: var(--text-sm)')
  })

  it('ställer siffrorna i en rak kolumn', () => {
    expect(rule('.match-card__time')).toContain('tabular-nums')
  })
})

describe('träffytan', () => {
  it('håller minst 44 px även om innehållet krymper', () => {
    expect(rule('.match-card')).toContain('min-height: var(--tap-target)')
  })
})
