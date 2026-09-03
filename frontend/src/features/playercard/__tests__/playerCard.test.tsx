import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import {
  decodeBackup,
  possessive,
  readCard,
  seasonFor,
  summarise,
  writeCard,
} from '@/features/playercard'
import { emptyCard, type Child, type MatchReport } from '@/features/playercard/storage/schema'
import { stubApi, testMatch, testTeams } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Barnets egen sida (`#46`).
 *
 * <para>
 * Ett samlarkort, inte ett kalkylark. Testerna vaktar de fyra sakerna som avgör om det
 * känns så: totalerna stämmer, varje match har en rad, säsongen sägs i ord, och ett barn
 * utan matcher möts av något annat än nollor.
 * </para>
 */

function child(id: string, name: string, extra: Partial<Child> = {}): Child {
  return { id, name, shirtNumber: null, teamSlug: null, seenBadges: [], ...extra }
}

function report(childId: string, values: Partial<MatchReport> = {}): MatchReport {
  return {
    id: `${childId}-${String(Math.random())}`,
    childId,
    matchId: null,
    playedUtc: '2026-09-20T12:00:00.000Z',
    goals: 0,
    assists: 0,
    teamGoals: null,
    opponentGoals: null,
    opponent: null,
    note: null,
    ...values,
  }
}

function season(reports: MatchReport[]) {
  return seasonFor({ ...emptyCard(), children: [child('1', 'Elias')], reports }, '1')
}

function openCard(reports: MatchReport[], name = 'Elias') {
  writeCard({ ...emptyCard(), children: [child('1', name)], reports })
  stubApi({})

  return renderRoute('/spelarkort/1')
}

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('totalerna', () => {
  it('räknar mål, assist, poäng och matcher', () => {
    const result = season([
      report('1', { goals: 2, assists: 1 }),
      report('1', { goals: 1, assists: 3 }),
    ])

    expect(result.totals.matches).toBe(2)
    expect(result.totals.goals).toBe(3)
    expect(result.totals.assists).toBe(4)
    expect(result.points).toBe(7)
  })

  it('visas överst på sidan', async () => {
    openCard([report('1', { goals: 2, assists: 1 })])

    await screen.findByRole('heading', { level: 1, name: 'Elias' })

    expect(screen.getByText('Poäng').nextSibling).toHaveTextContent('3')
  })
})

describe('lagets facit', () => {
  it('räknar vinst, oavgjort och förlust', () => {
    const result = season([
      report('1', { teamGoals: 3, opponentGoals: 1 }),
      report('1', { teamGoals: 2, opponentGoals: 2 }),
      report('1', { teamGoals: 0, opponentGoals: 4 }),
    ])

    expect(result.record).toEqual({ wins: 1, draws: 1, losses: 1 })
  })

  it('räknar inte en match där bara det ena målet är ifyllt', () => {
    /*
     * Ett halvt resultat sager ingenting om utgangen. Att lasa det tomma faltet som noll
     * hade hittat pa en vinst -- och facit ar det familjen sjalv skrivit, inte var gissning.
     */
    const result = season([report('1', { teamGoals: 3, opponentGoals: null })])

    expect(result.record).toEqual({ wins: 0, draws: 0, losses: 0 })
  })
})

describe('en rad per match', () => {
  it('visar motståndare, resultat och barnets insats', async () => {
    openCard([
      report('1', {
        opponent: 'Torslanda IK',
        teamGoals: 3,
        opponentGoals: 1,
        goals: 2,
        assists: 1,
      }),
    ])

    const rows = await screen.findAllByRole('listitem')
    const row = rows.find((candidate) => candidate.textContent?.includes('Torslanda IK'))

    expect(row).toBeDefined()
    expect(within(row!).getByLabelText('Resultat 3–1')).toBeInTheDocument()
    expect(row?.textContent).toContain('2')
  })

  it('visar datumet ensamt när rapporten inte minns motståndaren', async () => {
    /*
     * Rapporter fran fore version 4 bar bara datumet. Da star datumet ensamt i stallet
     * for att vi hittar pa ett lagnamn.
     */
    openCard([report('1', { goals: 1, opponent: null })])

    expect(await screen.findByText('Match')).toBeInTheDocument()
  })

  it('lägger senast spelade först', () => {
    const result = season([
      report('1', { goals: 1, playedUtc: '2026-04-01T12:00:00.000Z' }),
      report('1', { goals: 1, playedUtc: '2026-09-20T12:00:00.000Z' }),
    ])

    expect(result.rows[0]?.playedUtc).toBe('2026-09-20T12:00:00.000Z')
  })
})

describe('säsongen i klartext', () => {
  it('säger hur många matcher och vad det blivit', () => {
    const result = season([
      report('1', { goals: 2, assists: 1 }),
      report('1', { goals: 1, assists: 0 }),
    ])

    expect(summarise(result, 'Elias')).toContain('Elias har fyllt i 2 matcher.')
    expect(summarise(result, 'Elias')).toContain('Det har blivit 3 mål och 1 assist.')
  })

  it('böjer match i singular', () => {
    const result = season([report('1', { goals: 1 })])

    expect(summarise(result, 'Elias')).toContain('Elias har fyllt i 1 match.')
  })

  it('lyfter bästa matchen med motståndaren', () => {
    const result = season([
      report('1', { goals: 1 }),
      report('1', { goals: 3, opponent: 'Torslanda IK' }),
    ])

    expect(summarise(result, 'Elias')).toContain('Bästa matchen: 3 mål mot Torslanda IK.')
  })

  it('kallar inte ett ensamt mål för bästa matchen', () => {
    /*
     * En hojdpunkt ska sticka ut. "Basta matchen: 1 mal" i en sasong dar barnet gjort ett
     * mal per match ar ingen hojdpunkt, det ar sasongen.
     */
    const result = season([report('1', { goals: 1 })])

    expect(summarise(result, 'Elias').join(' ')).not.toContain('Bästa matchen')
  })

  it('säger inte 0 mål till den som inte gjort mål', () => {
    // Nollor ser ut som ett omdome. Den som bara har assist far lasa om sina assist.
    const result = season([report('1', { assists: 2 })])

    expect(summarise(result, 'Elias')).toContain('Det har blivit 2 assist.')
    expect(summarise(result, 'Elias').join(' ')).not.toContain('0 mål')
  })

  it('hittar inte på ett facit när inga resultat är ifyllda', () => {
    const result = season([report('1', { goals: 2 })])

    expect(summarise(result, 'Elias').join(' ')).not.toContain('vann laget')
  })
})

describe('tomt läge', () => {
  it('bjuder in till första matchen i stället för att visa nollor', async () => {
    openCard([])

    expect(await screen.findByText(/Första matchen är den roligaste/)).toBeInTheDocument()
    expect(screen.getByText(/Här kommer Elias matcher att synas/)).toBeInTheDocument()
    expect(screen.queryByText('Poäng')).not.toBeInTheDocument()
  })

  it('räknar inte en orörd rapport som en match', async () => {
    openCard([report('1')])

    expect(await screen.findByText(/Första matchen är den roligaste/)).toBeInTheDocument()
  })
})

describe('namnet i genitiv', () => {
  it.each([
    ['Elias', 'Elias matcher'],
    ['Lukas', 'Lukas matcher'],
    ['Alex', 'Alex matcher'],
    ['Vera', 'Veras matcher'],
    ['Nalle-Puh', 'Nalle-Puhs matcher'],
  ])('%s blir "%s"', (name, expected) => {
    /*
     * Ett namn som slutar pa s, x eller z far inget extra s. Elias, Lukas och Alex ar
     * vanliga i den har aldersgruppen, sa "Eliass matcher" hade synts direkt.
     */
    expect(`${possessive(name)} matcher`).toBe(expected)
  })
})

describe('en länk hit på fel telefon', () => {
  it('förklarar i stället för att visa ett tomt kort', async () => {
    /*
     * Id:t ar barnets lokala id och betyder ingenting utanfor den har telefonen. En delad
     * lank ska darfor sagas emot vanligt, inte rendera ett kort utan innehall.
     */
    writeCard({ ...emptyCard(), children: [child('1', 'Elias')] })
    stubApi({})

    renderRoute('/spelarkort/nagon-annans-id')

    expect(
      await screen.findByRole('heading', { name: 'Barnet finns inte här' }),
    ).toBeInTheDocument()
  })
})

describe('motståndaren skrivs av, den slås inte upp', () => {
  it('sparas på rapporten när matchrapporten fylls i', async () => {
    /*
     * Namnet skrivs av sa att barnets sida gar att lasa utan nat -- och sa att det inte
     * finns nagon vag ut fran spelarkortets filer (§KM.2).
     */
    writeCard({ ...emptyCard(), children: [child('1', 'Elias')] })
    stubApi({
      match: {
        team: testTeams[0]!,
        match: testMatch('match-1', '2026-09-20T12:00:00Z', { opponent: 'Torslanda IK' }),
      },
    })

    const user = userEvent.setup()
    renderRoute('/match/match-1')

    await user.click(await screen.findByRole('button', { name: 'Öka Mål — Elias' }))

    expect(readCard().reports[0]?.opponent).toBe('Torslanda IK')
  })

  it('räddas ur nyckeln när en gammal kod importeras', () => {
    /*
     * Foregangarens nyckel ar `datum_lag_motstandare`. Motstandaren gar alltsa att rada
     * ur den -- en importerad sasong behover inte tappa vem matcherna spelades mot.
     */
    const legacy =
      'KARRA1.' +
      btoa(
        String.fromCharCode(
          ...new TextEncoder().encode(
            JSON.stringify({
              kids: [{ id: 'k1', name: 'Elias', team: 'gul' }],
              stats: {
                '2026-09-20_gul_Torslanda IK': { us: 3, them: 1, kids: { k1: { g: 2, a: 1 } } },
              },
            }),
          ),
        ),
      )

    const result = decodeBackup(legacy)

    expect(result.ok && result.card.reports[0]?.opponent).toBe('Torslanda IK')
  })
})
