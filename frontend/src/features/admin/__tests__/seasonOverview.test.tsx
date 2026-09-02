import { render, screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { findClashes, SeasonOverview } from '@/features/admin'
import type { Match } from '@/features/matches'
import { testMatch } from '@/test/apiStub'

/**
 * Tränarens säsongsöversikt (`#41`).
 *
 * <para>
 * Överblicken är poängen. Det är i helheten en tränare upptäcker att två matcher krockar
 * — inte i nästa match-kortet, som per definition bara visar en.
 * </para>
 */

function render_(matches: Match[]) {
  return render(
    <SeasonOverview matches={matches} onEdit={vi.fn()} onCancel={vi.fn()} onDelete={vi.fn()} />,
  )
}

describe('krockar', () => {
  it('markerar två matcher som ligger för nära', () => {
    // Samma lag kan inte spela två matcher inom två timmar.
    const clashing = findClashes([
      testMatch('a', '2026-09-20T12:00:00Z'),
      testMatch('b', '2026-09-20T13:00:00Z'),
    ])

    expect(clashing.size).toBe(2)
  })

  it('larmar inte på ett tight men avsiktligt schema', () => {
    /*
     * I klubbens riktiga schema ligger fyra lag 75 minuter isar pa samma plan. Den har
     * vyn visar ett lag i taget, sa den enda matchen som rakas ar samma lag tva ganger --
     * men granzen far anda inte vara sa snav att en normal sondag ser ut som ett fel.
     */
    const calm = findClashes([
      testMatch('a', '2026-09-20T10:00:00Z'),
      testMatch('b', '2026-09-20T13:00:00Z'),
    ])

    expect(calm.size).toBe(0)
  })

  it('räknar inte en inställd match som krock', () => {
    // En inställd match tar ingen tid i anspråk. Att flagga den hade fått tränaren att
    // leta efter ett problem som inte finns.
    const clashing = findClashes([
      testMatch('a', '2026-09-20T12:00:00Z'),
      testMatch('b', '2026-09-20T12:30:00Z', { status: 'Cancelled' }),
    ])

    expect(clashing.size).toBe(0)
  })

  it('visar krocken på raden, inte bara i en ruta högst upp', () => {
    // En varning som inte säger vilken rad den gäller tvingar tränaren att leta själv.
    render_([
      testMatch('a', '2026-09-20T12:00:00Z', { opponent: 'Torslanda' }),
      testMatch('b', '2026-09-20T13:00:00Z', { opponent: 'Kareby' }),
    ])

    const row = screen.getByRole('row', { name: /Torslanda/ })

    expect(within(row).getByText('Krock')).toBeInTheDocument()
  })
})

describe('hela säsongen visas', () => {
  it('behåller spelade matcher', () => {
    // En lista som börjar vid dagens datum döljer just det tränaren letar efter.
    render_([
      testMatch('gammal', '2026-08-01T12:00:00Z', { opponent: 'Spelad match' }),
      testMatch('ny', '2026-10-01T12:00:00Z', { opponent: 'Kommande match' }),
    ])

    /*
     * Raden och inte texten: motstandaren star bade i cellen och i knapparnas dolda
     * namn ("Andra matchen mot Spelad match"), vilket ar hela poangen med dem.
     */
    expect(screen.getByRole('row', { name: /Spelad match/ })).toBeInTheDocument()
    expect(screen.getByRole('row', { name: /Kommande match/ })).toBeInTheDocument()
  })

  it('grupperar per månad', () => {
    render_([testMatch('a', '2026-09-20T12:00:00Z'), testMatch('b', '2026-10-04T12:00:00Z')])

    expect(screen.getByRole('heading', { name: 'September 2026' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Oktober 2026' })).toBeInTheDocument()
  })

  it('säger till när säsongen är tom', () => {
    render_([])

    expect(screen.getByText(/Inga matcher inlagda än/)).toBeInTheDocument()
  })
})

describe('snabbknappar per rad', () => {
  it('namnger knappen med matchen den gäller', () => {
    /*
     * "Andra" i en lista med trettio rader sager ingenting for den som lyssnar. Knappens
     * tillgangliga namn bar motstandaren, sa raderna gar att skilja at.
     */
    render_([testMatch('a', '2026-09-20T12:00:00Z', { opponent: 'Torslanda' })])

    expect(screen.getByRole('button', { name: 'Ändra matchen mot Torslanda' })).toBeInTheDocument()
  })

  it('erbjuder inte att ställa in en redan inställd match', () => {
    render_([
      testMatch('a', '2026-09-20T12:00:00Z', { opponent: 'Torslanda', status: 'Cancelled' }),
    ])

    expect(screen.queryByRole('button', { name: /Ställ in matchen/ })).not.toBeInTheDocument()
  })
})
