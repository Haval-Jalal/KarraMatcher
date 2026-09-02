import { act, renderHook, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { readCard, useMatchReports, writeCard } from '@/features/playercard'
import { emptyCard } from '@/features/playercard/storage/schema'
import { stubApi, testMatch, testTeams } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

// Källan läses som text för kopplingskontrollen, inte för beteendet.
import REPORT_SOURCE from '../MatchReportCard.tsx?raw'
import HOOK_SOURCE from '../useMatchReports.ts?raw'

/**
 * Matchrapporten (`#44`, §KM.2).
 *
 * <para>
 * En liten stund tillsammans efter matchen, inte en rapporteringsplikt. Testerna vaktar
 * de fyra saker som gör att den faktiskt blir ifylld: den sparar direkt, den går att
 * använda med en hand, den kan inte bli negativ, och ingenting av den lämnar telefonen.
 * </para>
 */

const MATCH_ID = 'match-1'

function child(id: string, name: string) {
  return { id, name, shirtNumber: null, teamSlug: null }
}

function openMatch(options: { cancelled?: boolean } = {}) {
  stubApi({
    match: {
      team: testTeams[0]!,
      match: testMatch(MATCH_ID, '2026-09-20T12:00:00Z', {
        ...(options.cancelled === true ? { status: 'Cancelled' as const } : {}),
      }),
    },
  })

  return renderRoute(`/match/${MATCH_ID}`)
}

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ingenting lämnar telefonen', () => {
  it('rapporten når lagringen, aldrig API-lagret', () => {
    // §KM.2. Kontrollen sitter på importen: vägen till ett nätverksanrop finns inte i
    // de här filerna, så statistiken kan inte skickas av misstag.
    for (const source of [REPORT_SOURCE, HOOK_SOURCE]) {
      expect(source).not.toContain('@/lib/api')
      expect(source).not.toContain('fetch(')
    }
  })
})

describe('sparas direkt utan sparaknapp', () => {
  it('skriver till enheten vid varje tryck', async () => {
    /*
     * En sparaknapp ar ett satt att forlora data. Rapporten fylls i med ett barn bredvid
     * sig, ofta pa vag ut ur en bil -- det som skrivs men aldrig sparas ar borta utan att
     * nagon marker det.
     */
    writeCard({ ...emptyCard(), children: [child('1', 'Elias')] })

    const user = userEvent.setup()
    openMatch()

    await user.click(await screen.findByRole('button', { name: 'Öka Mål — Elias' }))

    expect(readCard().reports[0]?.goals).toBe(1)
  })

  it('har ingen sparaknapp', async () => {
    writeCard({ ...emptyCard(), children: [child('1', 'Elias')] })

    openMatch()

    await screen.findByRole('heading', { name: 'Efter matchen' })

    expect(screen.queryByRole('button', { name: /Spara/ })).not.toBeInTheDocument()
  })
})

describe('kan aldrig bli negativt', () => {
  it('stänger av minus vid noll', async () => {
    // En spärr som syns är bättre än en som tyst rättar.
    writeCard({ ...emptyCard(), children: [child('1', 'Elias')] })

    openMatch()

    expect(await screen.findByRole('button', { name: 'Minska Mål — Elias' })).toBeDisabled()
  })

  it('går inte under noll när logiken anropas direkt', () => {
    /*
     * Provet som avslojade att det har behovdes: forsta versionen klickade pa minus i
     * granssnittet, dar knappen redan ar avstangd vid noll. Testet natte alltsa aldrig
     * logiken, och gick gront aven nar Math.max togs bort.
     *
     * Sparren ska galla oavsett vem som anropar -- granssnittet ar dar for att den ska
     * synas, inte for att den ska finnas.
     */
    const { result } = renderHook(() => useMatchReports(MATCH_ID))

    act(() => {
      result.current.adjust('1', 'goals', -1)
    })

    expect(readCard().reports[0]?.goals).toBe(0)
  })
})

describe('inställda matcher', () => {
  it('visar ingen inmatning', async () => {
    /*
     * En instalid match spelades aldrig. Ett inmatningsfalt dar hade bjudit in till att
     * fylla i nagot som inte hant, och en nolla i statistiken ar samre an ingen rad alls.
     */
    writeCard({ ...emptyCard(), children: [child('1', 'Elias')] })

    openMatch({ cancelled: true })

    await screen.findByRole('heading', { level: 1 })

    expect(screen.queryByRole('heading', { name: 'Efter matchen' })).not.toBeInTheDocument()
  })
})

describe('flera barn', () => {
  it('ger varje syskon en egen rad', async () => {
    writeCard({
      ...emptyCard(),
      children: [child('1', 'Elias'), child('2', 'Vera')],
    })

    const user = userEvent.setup()
    openMatch()

    await user.click(await screen.findByRole('button', { name: 'Öka Mål — Vera' }))

    const vera = readCard().reports.find((report) => report.childId === '2')
    const elias = readCard().reports.find((report) => report.childId === '1')

    expect(vera?.goals).toBe(1)
    expect(elias?.goals ?? 0).toBe(0)
  })
})

describe('resultatet', () => {
  it('sparas på varje syskons rapport', async () => {
    /*
     * Resultatet galler matchen, inte ett barn. Det skrivs anda pa varje syskons rapport
     * sa att en rapport ar fullstandig i sig sjalv -- tas ett barn bort ska den andres
     * rapport fortfarande veta hur matchen slutade.
     */
    writeCard({
      ...emptyCard(),
      children: [child('1', 'Elias'), child('2', 'Vera')],
    })

    const user = userEvent.setup()
    openMatch()

    await user.click(await screen.findByRole('button', { name: 'Öka Våra mål' }))

    const reports = readCard().reports

    expect(reports).toHaveLength(2)
    expect(reports.every((report) => report.teamGoals === 1)).toBe(true)
  })
})

describe('inga barn på enheten', () => {
  it('visar ingenting alls', async () => {
    // Den som bara vill se matchtiden ska inte mötas av en rapport att fylla i.
    openMatch()

    await screen.findByRole('heading', { level: 1 })

    expect(screen.queryByRole('heading', { name: 'Efter matchen' })).not.toBeInTheDocument()
  })
})
