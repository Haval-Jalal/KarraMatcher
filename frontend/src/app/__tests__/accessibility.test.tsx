import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { stubApi, testMatch, testTeams } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Tillgängligheten i den publika delen (§KM.0 A3).
 *
 * Mor- och farföräldrar är riktiga användare här, och några av dem använder skärmläsare
 * eller enbart tangentbord. Testerna nedan låser de krav som annars eroderar tyst — det
 * märks inte i en granskning att en knapp blivit en div, förrän någon inte kan använda den.
 */

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('sidtitel per vy', () => {
  it.each([
    ['/', 'Välj lag'],
    ['/finns-inte', 'Sidan finns inte'],
  ])('sätter en beskrivande titel för %s', async (path, expected) => {
    // WCAG 2.4.2 är nivå A. I en ensidesapp byts titeln inte av sig själv, så alla vyer
    // hette "Kärra Matcher" — vilket gör fliklistan, historiken och bokmärkena obrukbara.
    stubApi({})

    renderRoute(path)
    await screen.findByRole('heading', { level: 1 })

    expect(document.title).toContain(expected)
  })

  it('namnger laget i titeln på en lagsida', async () => {
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    renderRoute('/lag/gul')
    await screen.findByText('P2016 Gul')

    expect(document.title).toContain('P2016 Gul')
  })
})

describe('hoppa till innehållet', () => {
  it('erbjuder en hopplänk först i tabbordningen', async () => {
    // Lagväljaren upprepas på varje lagsida. Utan hopplänken måste den som navigerar med
    // tangentbord tabba förbi fyra länkar varje gång (WCAG 2.4.1).
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    const user = userEvent.setup()
    renderRoute('/lag/gul')
    await screen.findByText('P2016 Gul')

    await user.tab()

    expect(screen.getByRole('link', { name: 'Hoppa till innehållet' })).toHaveFocus()
  })

  it('pekar hopplänken på ett element som går att fokusera', async () => {
    // En hopplänk till ett element utan tabindex flyttar sidan men inte skärmläsaren.
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    renderRoute('/lag/gul')
    await screen.findByText('P2016 Gul')

    const target = document.getElementById('innehall')

    expect(target).not.toBeNull()
    expect(target).toHaveAttribute('tabindex', '-1')
  })
})

describe('lagväljaren är navigation, inte växlingsknappar', () => {
  it('använder länkar med riktiga adresser', async () => {
    // Att välja lag byter adress. En länk går att öppna i ny flik, kopiera och dela — en
    // knapp gör inget av det, och skärmläsaren säger fel sak om den.
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    renderRoute('/lag/gul')

    const nav = await screen.findByRole('navigation', { name: 'Välj lag' })
    const links = within(nav).getAllByRole('link')

    expect(links).toHaveLength(2)
    expect(links[0]).toHaveAttribute('href', '/lag/gul')
  })

  it('märker aktuellt lag med aria-current och inte aria-pressed', async () => {
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    renderRoute('/lag/gul')

    const current = await screen.findByRole('link', { name: /Gul/ })

    expect(current).toHaveAttribute('aria-current', 'page')
    expect(current).not.toHaveAttribute('aria-pressed')
  })

  it('markerar aktuellt lag med mer än färg', async () => {
    // WCAG 1.4.1. Bocken är den synliga signalen vid sidan av ramen och aria-current.
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    renderRoute('/lag/gul')

    expect(await screen.findByRole('link', { name: /Gul/ })).toHaveTextContent('✓')
    expect(screen.getByRole('link', { name: /Blå/ })).not.toHaveTextContent('✓')
  })
})

describe('allt går att nå med tangentbord', () => {
  it('når lagen, matchen och kalendern genom att tabba', async () => {
    stubApi({
      matches: {
        team: testTeams[0]!,
        matches: [testMatch('a', '2099-09-20T12:00:00Z')],
      },
    })

    const user = userEvent.setup()
    renderRoute('/lag/gul')
    await screen.findByText('P2016 Gul')

    const reachable: string[] = []

    for (let step = 0; step < 12; step++) {
      await user.tab()
      const active = document.activeElement

      if (active && active !== document.body) {
        reachable.push(active.textContent?.trim().slice(0, 30) ?? '')
      }
    }

    // Ingen fokuserbar kontroll får hoppas över: varje steg ska landa någonstans.
    expect(reachable.length).toBeGreaterThan(5)
    expect(reachable.join(' | ')).toContain('Hoppa till innehållet')
  })

  it('har inga positiva tabindex som bryter ordningen', async () => {
    // Ett positivt tabindex flyttar elementet före allt annat i tabbordningen och gör
    // ordningen omöjlig att förutse (WCAG 2.4.3).
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    renderRoute('/lag/gul')
    await screen.findByText('P2016 Gul')

    const positive = [...document.querySelectorAll('[tabindex]')].filter(
      (element) => Number(element.getAttribute('tabindex')) > 0,
    )

    expect(positive).toHaveLength(0)
  })
})

describe('sidan har den struktur en skärmläsare navigerar efter', () => {
  it('har exakt en h1', async () => {
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    renderRoute('/lag/gul')
    await screen.findByText('P2016 Gul')

    expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
  })

  it('hoppar inte över en rubriknivå', async () => {
    // Ett h3 direkt efter ett h1 låter en skärmläsaranvändare tro att något saknas.
    stubApi({
      matches: {
        team: testTeams[0]!,
        matches: [testMatch('a', '2099-09-20T12:00:00Z'), testMatch('b', '2099-10-04T12:00:00Z')],
      },
    })

    renderRoute('/lag/gul')
    await screen.findByText('P2016 Gul')

    const levels = screen.getAllByRole('heading').map((heading) => Number(heading.tagName.slice(1)))

    for (let i = 1; i < levels.length; i++) {
      expect(levels[i]! - levels[i - 1]!).toBeLessThanOrEqual(1)
    }
  })

  it('har ett main-landmärke', async () => {
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    renderRoute('/lag/gul')

    expect(await screen.findByRole('main')).toBeInTheDocument()
  })
})
