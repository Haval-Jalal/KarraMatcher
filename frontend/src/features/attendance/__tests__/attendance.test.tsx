import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { emptyResponse, jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Den riktade kallelsen per barn (§KM.7, `#199`).
 *
 * Det som vaktas: en vårdnadshavare ser sina egna kallade barn och svarar Ja/Nej per barn;
 * en gäst/avslagen kallelse (404) ser ingenting; en admin väljer barn tvärs över lagen och
 * skickar; summeringen räknar Ja/Nej/ej-svarat. Barn visas som "Liam J" (§KM.1).
 */

const TRUPP = 'trupp-1'
const EVENT = 'm1'
const FUTURE = '2026-12-20T11:00:00Z'

const PARENT_TOKEN = `x.${btoa('{"email":"foralder@example.com"}')}.y`
const ADMIN_TOKEN = `x.${btoa(JSON.stringify({ email: 'admin@example.com', 'admin-trupp': TRUPP }))}.y`
const COACH_TOKEN = `x.${btoa(JSON.stringify({ email: 'coach@example.com', coach: 'svart' }))}.y`

const team = { slug: 'svart', name: 'Svart', ageGroup: 'P2016', colorHex: '#161616' }

function eventDetail(truppWide = false) {
  return {
    team: truppWide ? null : team,
    truppId: TRUPP,
    truppName: 'P2016',
    event: {
      id: EVENT,
      type: 'Match',
      kickoffUtc: FUTURE,
      title: null,
      opponent: 'Torslanda',
      isHome: false,
      status: 'Scheduled',
      address: 'Klarebergsvallen',
      venue: {
        name: 'Klarebergsvallen',
        address: 'Klarebergsvallen',
        latitude: 57.8,
        longitude: 12,
      },
    },
  }
}

interface Options {
  token?: string
  mine?: Record<string, unknown> | 'gate-off'
  /** Låter vårdnadshavarens egen kallelse-GET hänga, så laddnings-platshållaren kan prövas (#396). */
  minePending?: boolean
  /** Låter vårdnadshavarens egen kallelse-GET svara 500, så fel-/retry-grenen kan prövas (#533). */
  mineError?: boolean
  summary?: unknown
  /** Låter adminens summerings-GET hänga, så racet "roster klar före summering" kan prövas (#392). */
  summaryPending?: boolean
  /** Låter vårdnadshavarens svar-PUT hänga, så per-barn-pending kan prövas (#592). */
  respondHang?: boolean
  /** Låter svar-PUT:en för just det här barnet svara 500, så per-barn-fel kan prövas (#592). */
  respondFailChildId?: string
  roster?: unknown
  /** Gör händelse-detaljen trupp-övergripande (utan lag), för #477. */
  truppWide?: boolean
}

function stub(options: Options) {
  const token = options.token ?? PARENT_TOKEN
  const sent: { url: string; method: string; body: unknown }[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      sent.push({
        url,
        method,
        body: typeof init?.body === 'string' ? JSON.parse(init.body) : null,
      })

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh'))
        return Promise.resolve(jsonResponse({ accessToken: token }))

      // Påminnelse
      if (url.includes('/remind')) return Promise.resolve(jsonResponse({ reminded: 2 }))

      // Vårdnadshavarens svar för ett barn: PUT /events/{id}/kallelse/children/{childId}
      if (url.includes('/kallelse/children/')) {
        if (options.respondHang) return new Promise<Response>(() => {})
        if (
          options.respondFailChildId !== undefined &&
          url.includes(`/children/${options.respondFailChildId}`)
        )
          return Promise.resolve(emptyResponse(500))
        return Promise.resolve(emptyResponse(204))
      }

      // Adminens kallelse (GET summering / PUT urval): /admin/.../kallelse
      if (url.includes('/admin/') && url.includes('/kallelse')) {
        if (method === 'PUT') return Promise.resolve(emptyResponse(204))
        // Hänger med flit: summeringen är ännu inte klar.
        if (options.summaryPending) return new Promise<Response>(() => {})
        return Promise.resolve(
          jsonResponse(
            options.summary ?? {
              callOpen: true,
              coming: 0,
              notComing: 0,
              notAnswered: 0,
              children: [],
            },
          ),
        )
      }

      // Tränarens väljar-roster (lag-scopad): GET /teams/{slug}/kallelse-roster
      if (url.includes('/kallelse-roster')) {
        return Promise.resolve(options.roster ?? jsonResponse({ teams: [], children: [] }))
      }

      // Tränarens kallelse (lag-scopad): GET summering / PUT urval på
      // /teams/{slug}/events/{id}/kallelse
      if (url.includes('/teams/') && url.includes('/events/') && url.includes('/kallelse')) {
        if (method === 'PUT') return Promise.resolve(emptyResponse(204))
        return Promise.resolve(
          jsonResponse(
            options.summary ?? {
              callOpen: true,
              coming: 0,
              notComing: 0,
              notAnswered: 0,
              children: [],
            },
          ),
        )
      }

      // Vårdnadshavarens vy: GET /events/{id}/kallelse
      if (url.includes('/kallelse')) {
        if (options.minePending) return new Promise<Response>(() => {})
        if (options.mineError) return Promise.resolve(emptyResponse(500))
        if (options.mine === 'gate-off') return Promise.resolve(emptyResponse(404))
        return Promise.resolve(
          jsonResponse(options.mine ?? { callOpen: true, kickoffUtc: FUTURE, children: [] }),
        )
      }

      // Truppens roster (adminens barnväljare): /admin/trupper/{id}/children
      if (url.includes('/children')) {
        return Promise.resolve(options.roster ?? jsonResponse({ teams: [], children: [] }))
      }

      if (url.includes('/carpool')) return Promise.resolve(jsonResponse([]))

      if (url.includes('/api/v1/events/'))
        return Promise.resolve(jsonResponse(eventDetail(options.truppWide)))

      return Promise.resolve(jsonResponse({}))
    }),
  )

  return sent
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('vårdnadshavaren svarar per barn', () => {
  it('skickar en gäst till inloggningen i stället för händelsen', async () => {
    // §KM.3: händelsen är truppens interna. En gäst når den inte — hen möts av
    // inloggningen, inte av ett halvt laddat kort eller ett 401.
    renderRoute(`/handelse/${EVENT}`)

    expect(await screen.findByRole('heading', { name: 'Logga in' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /Torslanda/ })).not.toBeInTheDocument()
  })

  it('en inloggad vars lag har kallelsen avslagen ser ingenting (404)', async () => {
    setAccessToken(PARENT_TOKEN)
    stub({ mine: 'gate-off' })

    renderRoute(`/handelse/${EVENT}`)

    expect(await screen.findByRole('heading', { name: /Torslanda/ })).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.queryByRole('heading', { name: 'Kallelse' })).not.toBeInTheDocument(),
    )
  })

  it('visar en laddnings-platshållare medan den egna kallelsen hämtas (#396)', async () => {
    // På en långsam uppkoppling får föräldern förr varken sektion eller spinner — en verklig
    // kallelse såg ut att saknas. Nu visas "Kallelse" med ett laddningsbesked medan frågan hämtas.
    setAccessToken(PARENT_TOKEN)
    stub({ minePending: true })

    renderRoute(`/handelse/${EVENT}`)

    expect(await screen.findByRole('heading', { name: /Torslanda/ })).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Kallelse' })).toBeInTheDocument()
    expect(await screen.findByText('Hämtar kallelsen…')).toBeInTheDocument()
  })

  it('visar fel med försök-igen i stället för att dölja hela sektionen vid 5xx (#533)', async () => {
    // Förr hanterades bara 404 (kallelsen avslagen). Vid ett nät-/serverfel försvann hela
    // "Kallelse"-sektionen tyst — en kallad förälder på dåligt nät såg ingenting.
    // useMyKallelse försöker om två gånger på ett icke-404-fel (backoff), så ge findBy extra tid
    // innan isError slår till.
    setAccessToken(PARENT_TOKEN)
    stub({ mineError: true })

    renderRoute(`/handelse/${EVENT}`)

    expect(
      await screen.findByRole('button', { name: 'Försök igen' }, { timeout: 8000 }),
    ).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Kallelse' })).toBeInTheDocument()
    expect(screen.getByText('Kunde inte hämta kallelsen just nu.')).toBeInTheDocument()
  }, 12000)

  it('ser sina egna kallade barn och svarar Ja', async () => {
    setAccessToken(PARENT_TOKEN)
    const sent = stub({
      mine: {
        callOpen: true,
        kickoffUtc: FUTURE,
        children: [{ childId: 'c1', displayName: 'Liam J', reply: null }],
      },
    })

    renderRoute(`/handelse/${EVENT}`)

    const group = await screen.findByRole('group', { name: 'Svar för Liam J' })
    await userEvent.click(within(group).getByRole('button', { name: 'Ja' }))

    await waitFor(() => {
      const put = sent.find(
        (r) => r.method === 'PUT' && r.url.includes(`/events/${EVENT}/kallelse/children/c1`),
      )
      expect(put?.body).toEqual({ reply: 'Coming' })
    })
  })

  it('fryser inte syskonens knappar medan ett barns svar är i flykt (#592)', async () => {
    // Förr delade alla barn en enda mutation: ett tryck för ett barn disablade hela listan hela
    // round-trippen (värst på kall backend). Nu är bara det svarande barnets knappar låsta.
    setAccessToken(PARENT_TOKEN)
    stub({
      respondHang: true,
      mine: {
        callOpen: true,
        kickoffUtc: FUTURE,
        children: [
          { childId: 'c1', displayName: 'Liam J', reply: null },
          { childId: 'c2', displayName: 'Nora K', reply: null },
        ],
      },
    })

    renderRoute(`/handelse/${EVENT}`)

    const groupA = await screen.findByRole('group', { name: 'Svar för Liam J' })
    const groupB = await screen.findByRole('group', { name: 'Svar för Nora K' })
    await userEvent.click(within(groupA).getByRole('button', { name: 'Ja' }))

    // Liams svar hänger → hans knappar låses. Noras förblir klickbara.
    await waitFor(() => expect(within(groupA).getByRole('button', { name: 'Ja' })).toBeDisabled())
    expect(within(groupA).getByRole('button', { name: 'Nej' })).toBeDisabled()
    expect(within(groupB).getByRole('button', { name: 'Ja' })).toBeEnabled()
    expect(within(groupB).getByRole('button', { name: 'Nej' })).toBeEnabled()
  })

  it('namnger vilket barns svar som misslyckades vid delfel (#592)', async () => {
    // Förr var felet en delad boolean: en förälder med flera barn såg inte vilket svar som föll.
    setAccessToken(PARENT_TOKEN)
    stub({
      respondFailChildId: 'c1',
      mine: {
        callOpen: true,
        kickoffUtc: FUTURE,
        children: [
          { childId: 'c1', displayName: 'Liam J', reply: null },
          { childId: 'c2', displayName: 'Nora K', reply: null },
        ],
      },
    })

    renderRoute(`/handelse/${EVENT}`)

    const groupA = await screen.findByRole('group', { name: 'Svar för Liam J' })
    await userEvent.click(within(groupA).getByRole('button', { name: 'Ja' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Liam J')
    // Syskonet drabbas inte: inget fel för Nora.
    expect(screen.queryByText(/Nora K: svaret gick inte/)).not.toBeInTheDocument()
  })

  it('invaliderar Hem-sammanställningen när ett svar sparas (#476)', async () => {
    setAccessToken(PARENT_TOKEN)
    stub({
      mine: {
        callOpen: true,
        kickoffUtc: FUTURE,
        children: [{ childId: 'c1', displayName: 'Liam J', reply: null }],
      },
    })

    const { queryClient } = renderRoute(`/handelse/${EVENT}`)
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    const group = await screen.findByRole('group', { name: 'Svar för Liam J' })
    await userEvent.click(within(group).getByRole('button', { name: 'Ja' }))

    // Hem räknar obesvarade kallelser — utan detta visar den ett inaktuellt antal i 60 s (#476).
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: ['home-summary'] }))
  })
})

describe('adminen skickar kallelse', () => {
  const roster = jsonResponse({
    teams: [
      { id: 't-svart', name: 'Svart', colorHex: '#161616' },
      { id: 't-gul', name: 'Gul', colorHex: '#D9A21B' },
    ],
    children: [
      {
        id: 'c1',
        firstName: 'Liam',
        lastInitial: 'J',
        displayName: 'Liam J',
        teamId: 't-svart',
        teamName: 'Svart',
        guardians: [],
      },
      {
        id: 'c2',
        firstName: 'Nora',
        lastInitial: 'K',
        displayName: 'Nora K',
        teamId: 't-gul',
        teamName: 'Gul',
        guardians: [],
      },
    ],
  })

  it('väljer barn tvärs över lagen och skickar', async () => {
    setAccessToken(ADMIN_TOKEN)
    const sent = stub({
      token: ADMIN_TOKEN,
      roster,
      summary: { callOpen: true, coming: 0, notComing: 0, notAnswered: 0, children: [] },
    })

    renderRoute(`/handelse/${EVENT}`)

    // Barnen grupperas per lag i en fieldset/legend, så en skärmläsare hör lagnamnet (#605).
    const gulGroup = await screen.findByRole('group', { name: 'Gul' })
    expect(within(gulGroup).getByLabelText('Nora K')).toBeInTheDocument()

    // Snabbval "Hela laget Svart" väljer Liam; sen kryssas en Gul-spelare in som fyllnad.
    await userEvent.click(await screen.findByRole('button', { name: 'Hela laget Svart' }))
    await userEvent.click(screen.getByLabelText('Nora K'))
    await userEvent.click(screen.getByRole('button', { name: 'Skicka kallelse' }))

    await waitFor(() => {
      const put = sent.find(
        (r) =>
          r.method === 'PUT' && r.url.includes(`/admin/trupper/${TRUPP}/events/${EVENT}/kallelse`),
      )
      const ids = (put?.body as { childIds: string[] } | undefined)?.childIds ?? []
      expect([...ids].sort()).toEqual(['c1', 'c2'])
    })

    // Skickandet kvitteras — annars vet adminen (och en skärmläsare) inte att det gick fram (#394).
    expect(await screen.findByText('Kallelsen är skickad.')).toBeInTheDocument()
  })

  it('varnar innan ett svarat barn tas bort ur kallelsen och skickar först efter bekräftelse (#472)', async () => {
    setAccessToken(ADMIN_TOKEN)
    const sent = stub({
      token: ADMIN_TOKEN,
      roster,
      summary: {
        callOpen: true,
        coming: 1,
        notComing: 0,
        notAnswered: 0,
        children: [
          {
            childId: 'c1',
            displayName: 'Liam J',
            teamName: 'Svart',
            colorHex: '#161616',
            reply: 'Coming',
          },
        ],
      },
    })

    renderRoute(`/handelse/${EVENT}`)

    // Liam är redan kallad och har svarat Ja → förkryssad. Avmarkera honom och skicka.
    await userEvent.click(await screen.findByLabelText('Liam J'))
    await userEvent.click(screen.getByRole('button', { name: 'Skicka kallelse' }))

    // Ingen PUT än — full synk skulle radera Liams svar, så en bekräftelse krävs först (#472).
    const warning = await screen.findByText(/tas bort ur kallelsen och förlorar sitt svar/)
    expect(warning).toHaveTextContent('Liam J')
    expect(sent.some((r) => r.method === 'PUT' && r.url.includes('/kallelse'))).toBe(false)

    // Bekräfta: nu skickas kallelsen utan Liam.
    await userEvent.click(screen.getByRole('button', { name: 'Skicka ändå' }))

    await waitFor(() => {
      const put = sent.find((r) => r.method === 'PUT' && r.url.includes('/kallelse'))
      const ids = (put?.body as { childIds: string[] } | undefined)?.childIds ?? []
      expect(put).toBeDefined()
      expect(ids).not.toContain('c1')
    })
  })

  it('döljer "Hela laget" på en trupp-övergripande händelse (#477)', async () => {
    setAccessToken(ADMIN_TOKEN)
    stub({
      token: ADMIN_TOKEN,
      roster,
      truppWide: true,
      summary: { callOpen: true, coming: 0, notComing: 0, notAnswered: 0, children: [] },
    })

    renderRoute(`/handelse/${EVENT}`)

    // "Hela truppen" finns kvar; "Hela laget {truppnamn}" döljs — den matchade förr noll barn
    // eftersom barnens teamName är färg-lag, aldrig truppnamnet (#477).
    expect(await screen.findByRole('button', { name: 'Hela truppen' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Hela laget/ })).not.toBeInTheDocument()
  })

  it('döljer skicka-knappen tills sammanställningen laddat — nollställer inte kallelsen (#392)', async () => {
    setAccessToken(ADMIN_TOKEN)
    const sent = stub({ token: ADMIN_TOKEN, roster, summaryPending: true })

    renderRoute(`/handelse/${EVENT}`)

    // Rostern hinner klart medan summeringen ännu hänger: panelen säger att svaren hämtas...
    expect(await screen.findByText('Hämtar svar…')).toBeInTheDocument()
    // ...men den destruktiva knappen och kryssrutorna finns inte förrän vi vet vilka som är
    // kallade. Utan grinden skulle ett tryck här full-synka en tom mängd och radera allt.
    expect(screen.queryByRole('button', { name: 'Skicka kallelse' })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Liam J')).not.toBeInTheDocument()
    // Ingen kallelse-PUT har skickats.
    expect(sent.some((r) => r.method === 'PUT' && r.url.includes('/kallelse'))).toBe(false)
  })

  it('visar sammanställningen och kan påminna', async () => {
    setAccessToken(ADMIN_TOKEN)
    const sent = stub({
      token: ADMIN_TOKEN,
      roster,
      summary: {
        callOpen: true,
        coming: 3,
        notComing: 1,
        notAnswered: 2,
        children: [
          {
            childId: 'c1',
            displayName: 'Liam J',
            teamName: 'Svart',
            colorHex: '#161616',
            reply: 'Coming',
          },
          {
            childId: 'c2',
            displayName: 'Nora K',
            teamName: 'Gul',
            colorHex: '#D9A21B',
            reply: null,
          },
        ],
      },
    })

    renderRoute(`/handelse/${EVENT}`)

    const heading = await screen.findByRole('heading', { name: 'Svar hittills' })
    const summary = heading.closest('.attendance__summary') as HTMLElement
    expect(screen.getByText(/3/, { selector: '.attendance__totals strong' })).toBeInTheDocument()
    // Liam J förekommer även i barnväljaren ovan — leta i just sammanställningen.
    expect(within(summary).getByText('Liam J')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Påminn dem som inte svarat' }))

    await waitFor(() =>
      expect(sent.some((r) => r.method === 'POST' && r.url.includes('/remind'))).toBe(true),
    )
    expect(await screen.findByText(/Påminde 2/)).toBeInTheDocument()
  })
})

describe('färg-lag-tränaren skickar kallelse', () => {
  const roster = jsonResponse({
    teams: [{ id: 't-svart', name: 'Svart', colorHex: '#161616' }],
    children: [
      {
        id: 'c1',
        firstName: 'Liam',
        lastInitial: 'J',
        displayName: 'Liam J',
        teamId: 't-svart',
        teamName: 'Svart',
        guardians: [],
      },
    ],
  })

  it('kvitterar att kallelsen skickades (#473)', async () => {
    setAccessToken(COACH_TOKEN)
    stub({
      token: COACH_TOKEN,
      roster,
      summary: { callOpen: true, coming: 0, notComing: 0, notAnswered: 0, children: [] },
    })

    renderRoute(`/handelse/${EVENT}`)

    await userEvent.click(await screen.findByRole('button', { name: 'Hela laget Svart' }))
    await userEvent.click(screen.getByRole('button', { name: 'Skicka kallelse' }))

    // Tränaren fick tidigare ingen bekräftelse och kunde skicka om i osäkerhet (#473).
    expect(await screen.findByText('Kallelsen är skickad.')).toBeInTheDocument()
  })
})

describe('kallelse-panelen scopas per trupp', () => {
  it('visar ingen (trasig) tränarpanel för en admin av en annan trupp på en trupp-vid händelse (#485)', async () => {
    const otherAdmin = `x.${btoa(JSON.stringify({ email: 'admin@example.com', 'admin-trupp': 'annan-trupp' }))}.y`
    setAccessToken(otherAdmin)
    const sent = stub({ token: otherAdmin, truppWide: true })

    renderRoute(`/handelse/${EVENT}`)

    // Händelsen renderas …
    await screen.findByRole('heading', { name: /Torslanda/ })
    // … men varken admin- eller tränarpanelen (ingen roll i den här truppen), och framför allt
    // ingen tränarpanel med tom lag-slug som skulle anropa /teams//kallelse-roster (#485).
    await waitFor(() =>
      expect(screen.queryByRole('heading', { name: 'Skicka kallelse' })).not.toBeInTheDocument(),
    )
    expect(sent.some((r) => r.url.includes('/teams//'))).toBe(false)
  })
})
