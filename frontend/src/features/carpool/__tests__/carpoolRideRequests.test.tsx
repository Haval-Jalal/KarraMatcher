import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { emptyResponse, jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Skjutsförfrågan — spegelbilden av att erbjuda skjuts (`#63`, §KM.12).
 *
 * <para>
 * En förälder som saknar skjuts ska inte behöva vänta på att någon lägger upp ett erbjudande: hen
 * kan be om skjuts, en förare erbjuder plats, och föräldern bekräftar. Testerna vaktar samma fyra
 * saker som åt andra hållet — att det går att be, att en förare kan erbjuda, att den som frågade
 * bekräftar, och att ett nekande inte kan bli tyst.
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

/** En skjutsförfrågan som API:t skulle ha levererat den. */
function rideRequest(overrides: Record<string, unknown> = {}) {
  return {
    id: 'rr1',
    direction: 'Both',
    seats: 1,
    note: 'Vi bor vid Skogomevägen.',
    status: 'Open',
    createdUtc: '2026-09-19T18:00:00Z',
    isMine: false,
    requesterName: 'Erik Lund',
    ...overrides,
  }
}

/** Ett platserbjudande på en förfrågan, som API:t skulle ha levererat det. */
function rideOffer(overrides: Record<string, unknown> = {}) {
  return {
    id: 'ro1',
    rideRequestId: 'rr1',
    seats: 1,
    message: 'Jag kör och har plats.',
    responseMessage: null,
    status: 'Pending',
    createdUtc: '2026-09-19T19:00:00Z',
    isMine: false,
    driverName: 'Anna Berg',
    ...overrides,
  }
}

/**
 * Svarar som API:t, och sparar det som skickades.
 *
 * Ordningen på kontrollerna spelar roll: de ride-specifika adresserna innehåller också
 * `/carpool/`, och `.../offers` finns både i erbjudande- och förfrågningsgrenen. De mer
 * specifika kontrolleras först.
 */
function stubApi(
  options: {
    rideRequests?: unknown[] | 'error'
    rideOffers?: unknown[]
    /** Låter platserbjudandena hänga, så race:en kan prövas. */
    rideOffersPending?: boolean
    /** Låter platserbjudandena falla (offline). */
    rideOffersError?: boolean
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

      // Den som frågade svarar på ett platserbjudande, eller föraren återtar sitt. 204 utan kropp.
      if (url.includes('/carpool/ride-offers/')) return Promise.resolve(emptyResponse(204))

      // En förfrågans platserbjudanden, eller att dra tillbaka förfrågan.
      if (url.includes('/carpool/ride-requests/')) {
        if (url.endsWith('/withdraw')) return Promise.resolve(emptyResponse(204))

        // POST .../offers: en förare erbjuder plats.
        if (method === 'POST') {
          return Promise.resolve(jsonResponse(rideOffer({ id: 'ny', isMine: true }), 201))
        }

        // GET .../offers.
        if (options.rideOffersPending) return new Promise<Response>(() => {})
        if (options.rideOffersError) return Promise.reject(new TypeError('Failed to fetch'))
        return Promise.resolve(jsonResponse(options.rideOffers ?? []))
      }

      // Listan över skjutsförfrågningar, eller att skapa en ny.
      if (url.includes('/carpool/ride-requests')) {
        if (options.rideRequests === 'error')
          return Promise.reject(new TypeError('Failed to fetch'))

        return Promise.resolve(
          method === 'POST'
            ? jsonResponse(rideRequest({ id: 'ny', isMine: true }), 201)
            : jsonResponse(options.rideRequests ?? []),
        )
      }

      // Erbjudande-sidan hålls tyst i de här testerna.
      if (url.includes('/carpool/offers')) return Promise.resolve(jsonResponse([]))

      // Detaljsidan hämtar händelsen på /api/v1/events/{id} (`#198`).
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

describe('skjutsförfrågningarna går att läsa av', () => {
  it('visar riktning, antal, vem som frågar och notisen', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ rideRequests: [rideRequest()] })

    renderRoute('/handelse/m1')

    expect(await screen.findByRole('heading', { name: 'Behöver skjuts' })).toBeInTheDocument()
    expect(await screen.findByText('Erik Lund frågar om skjuts')).toBeInTheDocument()
    expect(screen.getByText('Både till och hem')).toBeInTheDocument()
    expect(screen.getByText(/Vi bor vid Skogomevägen/)).toBeInTheDocument()
  })

  it('säger ifrån när ingen bett om skjuts', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ rideRequests: [] })

    renderRoute('/handelse/m1')

    expect(
      await screen.findByText('Ingen har bett om skjuts till den här matchen än.'),
    ).toBeInTheDocument()
  })

  it('säger att nätet är nere i stället för att visa en tom lista', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ rideRequests: 'error' })

    renderRoute('/handelse/m1')

    expect(
      await screen.findByText(/Skjutsförfrågningarna kan inte hämtas just nu/),
    ).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Torslanda/ })).toBeInTheDocument()
  })

  it('säger "Du frågar om skjuts" på den egna raden', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ rideRequests: [rideRequest({ isMine: true })] })

    renderRoute('/handelse/m1')

    expect(await screen.findByText('Du frågar om skjuts')).toBeInTheDocument()
  })
})

describe('en förälder ber om skjuts', () => {
  it('flyttar fokus in i formuläret när det öppnas, inte till <body> (#598)', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ rideRequests: [] })

    const user = userEvent.setup()
    renderRoute('/handelse/m1')

    await user.click(await screen.findByRole('button', { name: 'Fråga om skjuts' }))

    await waitFor(() => {
      const focused = document.activeElement as HTMLElement | null
      expect(focused?.tagName).toBe('INPUT')
      expect(focused).toHaveAttribute('name', 'direction')
    })
  })

  it('skickar förfrågan och invaliderar hela carpool-cachen (#495)', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    const sent = stubApi({ rideRequests: [] })

    const user = userEvent.setup()
    const { queryClient } = renderRoute('/handelse/m1')
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await user.click(await screen.findByRole('button', { name: 'Fråga om skjuts' }))

    await user.type(await screen.findByLabelText('Något mer att säga? (valfritt)'), 'Vid centrum')
    await user.click(screen.getByRole('button', { name: 'Fråga om skjuts' }))

    await waitFor(() => {
      const created = sent.find(
        (call) => call.method === 'POST' && call.url.endsWith('/ride-requests'),
      )

      expect(created?.body).toMatchObject({ direction: 'Both', seats: 1, note: 'Vid centrum' })
    })

    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: ['carpool'] }))
  })
})

describe('en förare erbjuder plats', () => {
  it('visar "Erbjud plats" och skickar erbjudandet', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    const sent = stubApi({ rideRequests: [rideRequest()], rideOffers: [] })

    const user = userEvent.setup()
    renderRoute('/handelse/m1')

    await user.click(await screen.findByRole('button', { name: 'Erbjud plats' }))

    await user.click(screen.getByRole('button', { name: 'Erbjud plats' }))

    await waitFor(() => {
      const offered = sent.find((call) => call.method === 'POST' && call.url.endsWith('/offers'))

      expect(offered?.body).toMatchObject({ seats: 1 })
    })
  })

  it('erbjuder inte "Erbjud plats" när läget inte kunde hämtas', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ rideRequests: [rideRequest()], rideOffersError: true })

    renderRoute('/handelse/m1')

    expect(
      await screen.findByText('Ingen anslutning. Platserbjudandena kan inte hämtas just nu.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Erbjud plats' })).not.toBeInTheDocument()
  })
})

describe('den som frågade bekräftar', () => {
  it('visar förarens erbjudande för den som frågade', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({ rideRequests: [rideRequest({ isMine: true })], rideOffers: [rideOffer()] })

    renderRoute('/handelse/m1')

    expect(await screen.findByText('Anna Berg erbjuder plats')).toBeInTheDocument()
    expect(screen.getByText(/Jag kör och har plats/)).toBeInTheDocument()
  })

  it('accepterar utan att behöva skriva något', async () => {
    // Ett ja behöver inga ord. Kravet på ord gäller nekandet.
    setAccessToken(SIGNED_IN_TOKEN)
    const sent = stubApi({
      rideRequests: [rideRequest({ isMine: true })],
      rideOffers: [rideOffer()],
    })

    const user = userEvent.setup()
    renderRoute('/handelse/m1')

    await user.click(await screen.findByRole('button', { name: 'Ja tack, jag åker med' }))

    await waitFor(() => {
      expect(sent.some((call) => call.url.includes('/accept'))).toBe(true)
    })
  })

  it('kräver ett meddelande för att neka, och erbjuder färdiga formuleringar', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    const sent = stubApi({
      rideRequests: [rideRequest({ isMine: true })],
      rideOffers: [rideOffer()],
    })

    const user = userEvent.setup()
    renderRoute('/handelse/m1')

    await user.click(await screen.findByRole('button', { name: 'Neka' }))

    // Ett tomt nekande stoppas innan det når servern (§KM.12).
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

  it('visar svaret vid ett nekande', async () => {
    setAccessToken(SIGNED_IN_TOKEN)
    stubApi({
      rideRequests: [rideRequest({ isMine: true })],
      rideOffers: [rideOffer({ status: 'Denied', responseMessage: 'Tyvärr, bilen blev full.' })],
    })

    renderRoute('/handelse/m1')

    expect(await screen.findByText('Nekad')).toBeInTheDocument()
    expect(screen.getByText(/Tyvärr, bilen blev full/)).toBeInTheDocument()
  })
})
