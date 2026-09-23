import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { emptyResponse, jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Samåkningsvyn (`#53`, §KM.12).
 *
 * <para>
 * Måttet är att det ska vara enklare än att skriva i föräldrachatten. Testerna vaktar de
 * fyra saker som annars gör att den inte används: att lediga platser går att läsa av utan
 * att räkna, att ett fullt erbjudande fortfarande går att fråga om, att ett nekande inte
 * kan bli tyst, och att en tid som skrivs i svensk tid blir rätt ögonblick.
 * </para>
 */

const SIGNED_IN_TOKEN = `x.${btoa('{"email":"foralder@example.com"}')}.y`

const match = {
  id: 'm1',
  type: 'Match',
  kickoffUtc: '2026-09-20T11:00:00Z',
  title: null,
  opponent: 'Torslanda',
  isHome: false,
  status: 'Scheduled',
  address: 'Klarebergsvallen, Göteborg',
  venue: {
    name: 'Klarebergsvallen',
    address: 'Klarebergsvallen, Göteborg',
    latitude: 57.8,
    longitude: 12,
  },
}

const team = { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' }

/** Ett erbjudande som API:t skulle ha levererat det. */
function offer(overrides: Record<string, unknown> = {}) {
  return {
    id: 'o1',
    matchId: 'm1',
    direction: 'ToMatch',
    departurePlace: 'Kärra centrum',
    departureUtc: '2026-09-20T10:15:00Z',
    seats: 3,
    seatsTaken: 1,
    seatsLeft: 2,
    isFull: false,
    note: null,
    isMine: false,
    driverName: 'Anna Berg',
    ...overrides,
  }
}

/** En förfrågan som API:t skulle ha levererat den. */
function request(overrides: Record<string, unknown> = {}) {
  return {
    id: 'r1',
    offerId: 'o1',
    seats: 1,
    message: 'Vi bor vid Skogomevägen.',
    responseMessage: null,
    status: 'Pending',
    createdUtc: '2026-09-19T18:00:00Z',
    isMine: false,
    requesterName: 'Erik Lund',
    ...overrides,
  }
}

/**
 * Svarar som API:t, och sparar det som skickades.
 *
 * Ordningen på kontrollerna spelar roll: samåkningens adresser innehåller också
 * `/api/v1/matches/`, så matchen får inte fångas först.
 */
function stubApi(
  options: {
    offers?: unknown[] | 'error'
    requests?: unknown[]
    matchDetail?: Record<string, unknown>
  } = {},
) {
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
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: SIGNED_IN_TOKEN }))
      }

      // Inloggningen, for gastens vag in. 202 utan kropp ar vad servern faktiskt svarar.
      if (url.includes('/auth/request-code')) return Promise.resolve(emptyResponse(202))
      if (url.includes('/auth/verify-code')) {
        return Promise.resolve(jsonResponse({ accessToken: SIGNED_IN_TOKEN }))
      }

      if (url.includes('/carpool/requests/')) return Promise.resolve(emptyResponse(204))
      if (url.includes('/withdraw')) return Promise.resolve(emptyResponse(204))

      if (url.includes('/carpool/offers/')) {
        return Promise.resolve(
          method === 'POST'
            ? jsonResponse(request({ id: 'ny', isMine: true }), 201)
            : jsonResponse(options.requests ?? []),
        )
      }

      if (url.includes('/carpool/offers')) {
        if (options.offers === 'error') return Promise.reject(new TypeError('Failed to fetch'))

        return Promise.resolve(
          method === 'POST'
            ? jsonResponse(offer({ id: 'ny', isMine: true }), 201)
            : jsonResponse(options.offers ?? []),
        )
      }

      // Detaljsidan hämtar händelsen på /api/v1/events/{id} (`#198`). Samåkningens egna
      // adresser ligger kvar under /api/v1/matches/ och fångas ovan.
      if (url.includes('/api/v1/events/')) {
        return Promise.resolve(jsonResponse(options.matchDetail ?? { team, event: match }))
      }

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

describe('erbjudandena går att läsa av', () => {
  it('visar lediga platser, avgångstid och avgångsplats', async () => {
    // Tiden lagras i UTC och visas i svensk tid (§KM.5). Testerna kör i
    // America/Los_Angeles, så en implementation som använder webbläsarens zon faller här.
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ offers: [offer()] })

    renderRoute('/handelse/m1')

    expect(await screen.findByRole('heading', { name: 'Samåkning' })).toBeInTheDocument()
    expect(await screen.findByText('2 platser kvar')).toBeInTheDocument()
    expect(screen.getByText('12:15')).toBeInTheDocument()
    expect(screen.getByText('Från Kärra centrum')).toBeInTheDocument()
    expect(screen.getByText('Till matchen')).toBeInTheDocument()
  })

  it('säger ifrån när ingen erbjudit skjuts', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ offers: [] })

    renderRoute('/handelse/m1')

    expect(
      await screen.findByText('Ingen har erbjudit skjuts till den här matchen än.'),
    ).toBeInTheDocument()
  })

  it('säger att nätet är nere i stället för att visa en tom lista', async () => {
    // Appen används på fotbollsplaner med dålig täckning. Resten av matchen står kvar.
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ offers: 'error' })

    renderRoute('/handelse/m1')

    expect(await screen.findByText(/Ingen anslutning/)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Torslanda/ })).toBeInTheDocument()
  })
})

describe('samåkningen har ett ansikte', () => {
  it('säger vem som kör', async () => {
    /*
     * `#154`. Fore namnen sa kortet ingenting om vem som satt bakom ratten, och en
     * forfragan gick till "nagon". Mellan grannar som mots pa planen nasta lordag ar det
     * ett tomrum.
     */
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ offers: [offer()] })

    renderRoute('/handelse/m1')

    expect(await screen.findByText('Anna Berg kör')).toBeInTheDocument()
  })

  it('säger "Du kör" på den egna raden', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ offers: [offer({ isMine: true })] })

    renderRoute('/handelse/m1')

    expect(await screen.findByText('Du kör')).toBeInTheDocument()
  })

  it('säger inget alls när kontot saknar namn', async () => {
    // Konton skapade fore `#154` har inget. "Okand kor" hade latit som ett fel.
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ offers: [offer({ driverName: null })] })

    renderRoute('/handelse/m1')

    expect(await screen.findByText('2 platser kvar')).toBeInTheDocument()
    expect(screen.queryByText(/kör$/)).not.toBeInTheDocument()
  })

  it('visar vem som frågar, för föraren som ska svara', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ offers: [offer({ isMine: true })], requests: [request()] })

    renderRoute('/handelse/m1')

    expect(await screen.findByText('Erik Lund frågar om skjuts')).toBeInTheDocument()
  })
})

describe('ett fullt erbjudande är märkt men inte stängt', () => {
  it('går fortfarande att fråga om', async () => {
    /*
     * §KM.12 rakt av. Doldes knappen när bilen är full mötte den som frågar en död knapp
     * i stället för ett svar från en granne — och det är just det appen ska ersätta.
     */
    setAccessToken(SIGNED_IN_TOKEN)
    const sent = stubApi({
      offers: [offer({ seats: 2, seatsTaken: 2, seatsLeft: 0, isFull: true })],
    })

    const user = userEvent.setup()
    renderRoute('/handelse/m1')

    expect(await screen.findByText('Fullt')).toBeInTheDocument()

    await user.click(await screen.findByRole('button', { name: 'Fråga om plats' }))

    expect(await screen.findByText(/Bilen är full just nu/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Skicka förfrågan' }))

    await waitFor(() => {
      const asked = sent.find((call) => call.method === 'POST' && call.url.endsWith('/requests'))

      expect(asked?.body).toMatchObject({ seats: 1 })
    })
  })
})

describe('ett nekande kan inte bli tyst', () => {
  it('kräver ett meddelande och erbjuder färdiga formuleringar', async () => {
    /*
     * Kravet finns även server-side. Det här testet vaktar den halvan som avgör om det
     * blir av: att föraren får ord att trycka på i stället för en tom ruta.
     */
    setAccessToken(SIGNED_IN_TOKEN)
    const sent = stubApi({
      offers: [offer({ isMine: true })],
      requests: [request()],
    })

    const user = userEvent.setup()
    renderRoute('/handelse/m1')

    await user.click(await screen.findByRole('button', { name: 'Neka' }))

    // Ett tomt nekande stoppas innan det når servern.
    await user.click(screen.getByRole('button', { name: 'Skicka nekande' }))

    expect(await screen.findByText(/ett tyst nej ska inte förekomma/)).toBeInTheDocument()
    expect(sent.some((call) => call.url.includes('/deny'))).toBe(false)

    await user.click(screen.getByRole('button', { name: 'Någon annan hann före.' }))
    await user.click(screen.getByRole('button', { name: 'Skicka nekande' }))

    await waitFor(() => {
      const denial = sent.find((call) => call.url.includes('/deny'))

      expect(denial?.body).toEqual({ message: 'Någon annan hann före.' })
    })
  })

  it('visar förarens svar för den som frågade', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({
      offers: [offer()],
      requests: [
        request({ status: 'Denied', isMine: true, responseMessage: 'Bilen är tyvärr full.' }),
      ],
    })

    renderRoute('/handelse/m1')

    expect(await screen.findByText('Nekad')).toBeInTheDocument()
    expect(screen.getByText(/Bilen är tyvärr full/)).toBeInTheDocument()
  })
})

describe('att lägga upp en skjuts', () => {
  it('skickar svensk tid som UTC, även efter sommartidsskiftet', async () => {
    /*
     * Säsongen sträcker sig förbi skiftet i oktober (§KM.5). Efter det ligger Sverige en
     * timme före UTC, inte två — en omräkning med fast offset blir fel här, och då står
     * någon på fel plats vid fel klockslag.
     */
    setAccessToken(SIGNED_IN_TOKEN)
    const sent = stubApi({
      offers: [],
      matchDetail: { team, event: { ...match, kickoffUtc: '2026-10-31T13:00:00Z' } },
    })

    const user = userEvent.setup()
    renderRoute('/handelse/m1')

    await user.click(await screen.findByRole('button', { name: 'Erbjud skjuts' }))

    await user.type(await screen.findByLabelText('Var åker ni ifrån?'), 'Kärra centrum')

    const departure = screen.getByLabelText('Avgång (svensk tid)')

    await user.clear(departure)
    await user.type(departure, '2026-10-31T13:15')

    await user.click(screen.getByRole('button', { name: 'Lägg upp' }))

    await waitFor(() => {
      const created = sent.find((call) => call.method === 'POST' && call.url.endsWith('/offers'))

      expect(created?.body).toMatchObject({
        departureUtc: '2026-10-31T12:15:00.000Z',
        departurePlace: 'Kärra centrum',
        direction: 'ToMatch',
        seats: 1,
      })
    })
  })
})

describe('gästen kommer inte in', () => {
  it('skickas till inloggningen i stället för händelsesidan', async () => {
    /*
     * §KM.3, `#242`: händelsesidan är stängd. En gäst möts av inloggningen direkt, inte av
     * ett halvt laddat kort eller en knapp som svarar 401 — båda får någon att ge upp och
     * skriva i gruppchatten i stället. Vägen tillbaka via `next` prövas i routing-testet.
     */
    renderRoute('/handelse/m1')

    expect(await screen.findByRole('heading', { name: 'Logga in' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /Torslanda/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Erbjud skjuts' })).not.toBeInTheDocument()
  })
})

describe('föraren svarar på sina förfrågningar', () => {
  it('accepterar utan att behöva skriva något', async () => {
    // Ett ja behöver inga ord. Kravet på ord gäller nekandet.
    setAccessToken(SIGNED_IN_TOKEN)
    const sent = stubApi({ offers: [offer({ isMine: true })], requests: [request()] })

    const user = userEvent.setup()
    renderRoute('/handelse/m1')

    expect(await screen.findByText('Väntar på svar')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Ja tack, häng med' }))

    await waitFor(() => {
      expect(sent.some((call) => call.url.includes('/accept'))).toBe(true)
    })
  })
})
