import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import {
  BADGES,
  decodeBackup,
  earnedBadges,
  readCard,
  totalsFor,
  unseenBadges,
  writeCard,
} from '@/features/playercard'
import { emptyCard, type Child, type MatchReport } from '@/features/playercard/storage/schema'
import { stubApi, testMatch, testTeams } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'
import CSS from '@/styles/index.css?raw'

/**
 * Märkena (`#45`, §KM.0 A3).
 *
 * <para>
 * Det är märkena som gör att barnet vill öppna appen igen. De sex är avlästa ur
 * föregångaren — samma namn, samma trösklar — eftersom familjerna redan har en säsong
 * bakom sig och ett barn som låst upp Hattrick inte ska behöva göra om det.
 * </para>
 */

const MATCH_ID = 'match-1'

function child(id: string, name: string, seenBadges: string[] = []): Child {
  return { id, name, shirtNumber: null, teamSlug: null, seenBadges }
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

function cardWith(reports: MatchReport[], seen: string[] = []) {
  return { ...emptyCard(), children: [child('1', 'Elias', seen)], reports }
}

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('märken räknas ur den lokala statistiken', () => {
  it.each([
    ['forsta-malet', [report('1', { goals: 1 })]],
    ['passningskung', [report('1', { assists: 3 }), report('1', { assists: 2 })]],
    ['hattrick', [report('1', { goals: 3 })]],
    ['malmaskin', [report('1', { goals: 6 }), report('1', { goals: 4 })]],
    ['poangstjarna', [report('1', { goals: 6, assists: 4 })]],
    ['stammis', Array.from({ length: 10 }, () => report('1', { teamGoals: 1, opponentGoals: 0 }))],
  ])('%s låses upp precis på tröskeln', (id, reports) => {
    const earned = earnedBadges(totalsFor(cardWith(reports), '1'))

    expect(earned.map((badge) => badge.id)).toContain(id)
  })

  it('ger hattrick för tre mål i EN match, inte tre mål ihop', () => {
    /*
     * Tre mal utspridda over tre matcher ar inte ett hattrick, och att rakna dem som ett
     * hade gjort market meningslost -- foregangaren mater maxG av samma skal.
     */
    const spread = cardWith([
      report('1', { goals: 1 }),
      report('1', { goals: 1 }),
      report('1', { goals: 1 }),
    ])

    expect(earnedBadges(totalsFor(spread, '1')).map((badge) => badge.id)).not.toContain('hattrick')
  })

  it('räknar inte en tom rapport som en spelad match', () => {
    /*
     * En tom rapport blir till sa fort nagon ror en knapp och angrar sig. Stammis ska
     * betyda tio matcher, inte tio felklick.
     */
    const empty = cardWith(Array.from({ length: 10 }, () => report('1')))

    expect(totalsFor(empty, '1').matches).toBe(0)
  })

  it('blandar inte ihop syskon', () => {
    const card = {
      ...emptyCard(),
      children: [child('1', 'Elias'), child('2', 'Vera')],
      reports: [report('2', { goals: 5 })],
    }

    expect(totalsFor(card, '1').goals).toBe(0)
    expect(totalsFor(card, '2').goals).toBe(5)
  })
})

describe('firandet sker när märket låses upp', () => {
  function openMatch() {
    stubApi({
      match: { team: testTeams[0]!, match: testMatch(MATCH_ID, '2026-09-20T12:00:00Z') },
    })

    return renderRoute(`/match/${MATCH_ID}`)
  }

  it('dyker upp i matchrapporten när målet fylls i', async () => {
    writeCard({ ...emptyCard(), children: [child('1', 'Elias')] })

    const user = userEvent.setup()
    openMatch()

    expect(screen.queryByText('Nytt märke!')).not.toBeInTheDocument()

    await user.click(await screen.findByRole('button', { name: 'Öka Mål — Elias' }))

    expect(await screen.findByText('Nytt märke!')).toBeInTheDocument()
    expect(screen.getByText(/låste upp Första målet/)).toBeInTheDocument()
  })

  it('firas en gång, inte vid varje omstart', async () => {
    /*
     * Utan det har hade samma marke firats varje gang matchen oppnades, och en handelse
     * blivit en paminnelse man klickar bort.
     */
    writeCard({ ...emptyCard(), children: [child('1', 'Elias')] })

    const user = userEvent.setup()
    const view = openMatch()

    await user.click(await screen.findByRole('button', { name: 'Öka Mål — Elias' }))
    await user.click(await screen.findByRole('button', { name: 'Så bra!' }))

    expect(screen.queryByText('Nytt märke!')).not.toBeInTheDocument()
    expect(readCard().children[0]?.seenBadges).toContain('forsta-malet')

    view.unmount()
    openMatch()

    await screen.findByRole('heading', { level: 1 })

    expect(screen.queryByText('Nytt märke!')).not.toBeInTheDocument()
  })

  it('firar nästa märke också', async () => {
    writeCard(cardWith([report('1', { goals: 1 })], ['forsta-malet']))

    const user = userEvent.setup()
    openMatch()

    // Tre mal i den har matchen ger hattrick.
    const plus = await screen.findByRole('button', { name: 'Öka Mål — Elias' })

    await user.click(plus)
    await user.click(plus)
    await user.click(plus)

    expect(await screen.findByText(/låste upp Hattrick/)).toBeInTheDocument()
  })
})

describe('låsta märken visas som låsta', () => {
  async function openBadges() {
    // Marken bor pa barnets egen sida (#46), dar de star bredvid siffrorna de raknas ur.
    stubApi({})
    renderRoute('/spelarkort/1')

    await screen.findByRole('heading', { level: 1, name: 'Elias' })
  }

  it('listar alla sex, med krav och hur långt barnet kommit', async () => {
    writeCard(cardWith([report('1', { goals: 1, assists: 2 })]))

    await openBadges()

    for (const badge of BADGES) {
      expect(screen.getByText(badge.name)).toBeInTheDocument()
    }

    // Tva av fem assist mot Passningskung.
    expect(screen.getByText('2 av 5')).toBeInTheDocument()
  })

  it('säger låst i ord, inte bara med ljushet', async () => {
    /*
     * Foregangaren dampade lasta marken med opacity .4 och inget annat. Det bar bade
     * beskedet med ljushet ensam (WCAG 1.4.1) och faller under 4,5:1 -- har star det i
     * text som en skarmlasare kan lasa upp.
     */
    writeCard(cardWith([report('1', { goals: 1 })]))

    await openBadges()

    expect(screen.getByText(/Första målet — upplåst/)).toBeInTheDocument()
    expect(screen.getByText(/Hattrick — låst/)).toBeInTheDocument()
  })
})

describe('en hel säsong firas inte i efterhand', () => {
  it('migreringen 2 till 3 markerar det redan förtjänade som sett', () => {
    /*
     * En familj som spelat en hel sasong ska inte motas av sex firanden vid en
     * uppdatering. Det firar ingenting som just hant, och det forsta riktiga firandet
     * drunknar.
     */
    localStorage.setItem(
      'karra.spelarkort',
      JSON.stringify({
        version: 2,
        children: [{ id: '1', name: 'Elias', shirtNumber: null, teamSlug: null }],
        reports: [report('1', { goals: 12, assists: 6 })],
        lastBackupUtc: null,
      }),
    )

    const card = readCard()

    expect(card.version).toBe(4)
    expect(unseenBadges(card, '1')).toHaveLength(0)
    expect(card.children[0]?.seenBadges).toContain('malmaskin')
  })

  it('en kod från den gamla appen firas inte heller i efterhand', () => {
    const legacy = code({
      kids: [{ id: 'k1', name: 'Elias', team: 'gul' }],
      stats: { '2026-09-20_gul_Torslanda': { us: 3, them: 1, kids: { k1: { g: 3, a: 1 } } } },
    })

    const result = decodeBackup(`KARRA1.${legacy}`)

    expect(result.ok).toBe(true)
    expect(result.ok && unseenBadges(result.card, 'k1')).toHaveLength(0)
    expect(result.ok && result.card.children[0]?.seenBadges).toContain('hattrick')
  })

  it('en säkerhetskopia från version 2 migreras i stället för att skrivas ner som aktuell', () => {
    /*
     * Koden bar sin egen version. En kod sparad forra sasongen ar precis det som ska ga
     * att lasa -- gar den forbi migreringskedjan skrivs den ner som aktuell utan att vara
     * det, och faltet saknas forst nar nagot laser det.
     */
    const old = code({
      version: 2,
      children: [{ id: '1', name: 'Elias', shirtNumber: null, teamSlug: null }],
      reports: [report('1', { goals: 2 })],
      lastBackupUtc: null,
    })

    const result = decodeBackup(`KARRA2.${old}`)

    expect(result.ok && result.card.version).toBe(4)
    expect(result.ok && result.card.children[0]?.seenBadges).toEqual(['forsta-malet'])
  })
})

describe('rörelsen är tillagd, inte bortstädad', () => {
  const GUARD = '@media (prefers-reduced-motion: no-preference)'

  it('firandets animation ligger bakom prefers-reduced-motion: no-preference', () => {
    /*
     * Regeln langst ned i stilmallen nollar alla animationer under reduced-motion, men
     * den ar ett skyddsnat. Studsen ska vara opt-in: den som inte uttryckt nagot om
     * rorelse ar den enda som far den.
     */
    expect(CSS).toContain(GUARD)

    const block = CSS.slice(CSS.indexOf(GUARD))

    expect(block).toContain('animation: celebration-in')
    expect(block).toContain('animation: celebration-pop')
  })

  it('har ingen animation på firandet utanför det blocket', () => {
    const before = CSS.slice(0, CSS.indexOf(GUARD))

    expect(before).toContain('.celebration {')
    expect(before.slice(before.indexOf('.celebration {'))).not.toContain('animation:')
  })
})

function code(payload: unknown): string {
  return btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(payload))))
}
