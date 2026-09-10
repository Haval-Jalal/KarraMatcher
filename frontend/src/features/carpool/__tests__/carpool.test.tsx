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
  kickoffUtc: '2026-09-20T11:00:00Z',
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

      if (url.includes('/api/v1/matches/')) {
        return Promise.resolve(jsonResponse(options.matchDetail ?? { match, team }))
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
    stubApi({ offers: [offer()] })

    renderRoute('/match/m1')

    expect(await screen.findByRole('heading', { name: 'Samåkning' })).toBeInTheDocument()
    expect(await screen.findByText('2 platser kvar')).toBeInTheDocument()
    expect(screen.getByText('12:15')).toBeInTheDocument()
    expect(screen.getByText('Från Kärra centrum')).toBeInTheDocument()
    expect(screen.getByText('Till matchen')).toBeInTheDocument()
  })

  it('säger ifrån när ingen erbjudit skjuts', async () => {
    stubApi({ offers: [] })

    renderRoute('/match/m1')

    expect(
      await screen.findByText('Ingen har erbjudit skjuts till den här matchen än.'),
    ).toBeInTheDocument()
  })

  it('säger att nätet är nere i stället för att visa en tom lista', async () => {
    // Appen används på fotbollsplaner med dålig täckning. Resten av matchen står kvar.
    stubApi({ offers: 'error' })

    renderRoute('/match/m1')

    expect(await screen.findByText(/Ingen anslutning/)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Torslanda/ })).toBeInTheDocument()
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
    renderRoute('/match/m1')

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
    renderRoute('/match/m1')

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

    renderRoute('/match/m1')

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
      matchDetail: { match: { ...match, kickoffUtc: '2026-10-31T13:00:00Z' }, team },
    })

    const user = userEvent.setup()
    renderRoute('/match/m1')

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

  it('erbjuder inte gästen att lägga upp något', async () => {
    // Gästen ser samåkningen men deltar inte (§KM.3). Vägen in byggs i `#54`.
    stubApi({ offers: [offer()] })

    renderRoute('/match/m1')

    expect(await screen.findByText('2 platser kvar')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Erbjud skjuts' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Fråga om plats' })).not.toBeInTheDocument()
  })
})

describe('föraren svarar på sina förfrågningar', () => {
  it('accepterar utan att behöva skriva något', async () => {
    // Ett ja behöver inga ord. Kravet på ord gäller nekandet.
    setAccessToken(SIGNED_IN_TOKEN)
    const sent = stubApi({ offers: [offer({ isMine: true })], requests: [request()] })

    const user = userEvent.setup()
    renderRoute('/match/m1')

    expect(await screen.findByText('Väntar på svar')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Ja tack, häng med' }))

    await waitFor(() => {
      expect(sent.some((call) => call.url.includes('/accept'))).toBe(true)
    })
  })
})
