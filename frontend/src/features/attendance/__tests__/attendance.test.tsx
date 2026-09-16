import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { emptyResponse, jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Kallelsen på matchsidan (`#57`, §KM.7).
 *
 * Det som vaktas: att funktionen är osynlig när grinden är av (404), att tränaren ser
 * knappen att kalla, att en vuxen kan svara med ett antal, och att "kan inte" inte frågar
 * efter ett antal. Inget barn nämns någonstans (§KM.1).
 */

const PARENT_TOKEN = `x.${btoa('{"email":"foralder@example.com"}')}.y`

function coachToken(slug: string): string {
  return `x.${btoa(JSON.stringify({ email: 'tranare@example.com', coach: slug }))}.y`
}

const team = { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' }

function matchAt(kickoffUtc: string) {
  return {
    id: 'm1',
    kickoffUtc,
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
}

const FUTURE = '2026-12-20T11:00:00Z'
const PAST = '2026-01-10T11:00:00Z'

interface StateBody {
  callOpen: boolean
  kickoffUtc: string
  myResponse: { status: string; count: number; updatedUtc: string } | null
}

/**
 * Svarar som API:t. `/api/v1/matches/m1/attendance` innehåller också `/api/v1/matches/`,
 * så kallelsens adresser måste fångas före matchen.
 */
function stubApi(options: {
  token?: string
  state?: StateBody | 'gate-off'
  kickoffUtc?: string
  summary?: unknown
}) {
  const token = options.token ?? PARENT_TOKEN
  const kickoffUtc = options.kickoffUtc ?? FUTURE
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

      if (url.includes('/attendance/summary')) {
        return Promise.resolve(
          jsonResponse(
            options.summary ?? {
              comingPeople: 0,
              maybePeople: 0,
              cantComeFamilies: 0,
              respondedFamilies: 0,
              responders: [],
            },
          ),
        )
      }

      if (url.includes('/attendance/remind')) {
        return Promise.resolve(jsonResponse({ reminded: 2 }))
      }

      if (url.includes('/attendance/call')) return Promise.resolve(emptyResponse(204))
      if (url.includes('/attendance/response')) return Promise.resolve(emptyResponse(204))

      if (url.includes('/attendance')) {
        if (options.state === 'gate-off') return Promise.resolve(emptyResponse(404))

        return Promise.resolve(
          jsonResponse(options.state ?? { callOpen: false, kickoffUtc, myResponse: null }),
        )
      }

      if (url.includes('/carpool')) return Promise.resolve(jsonResponse([]))

      if (url.includes('/api/v1/matches/')) {
        return Promise.resolve(jsonResponse({ match: matchAt(kickoffUtc), team }))
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

describe('grinden håller funktionen osynlig', () => {
  it('en gäst ser ingen kallelse', async () => {
    stubApi({ state: { callOpen: true, kickoffUtc: FUTURE, myResponse: null } })

    renderRoute('/match/m1')

    // Matchen laddar; kallelsen ska aldrig dyka upp for en utloggad.
    expect(await screen.findByRole('heading', { name: /Torslanda/ })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Kallelse' })).not.toBeInTheDocument()
  })

  it('en inloggad vars lag har kallelsen avslagen ser ingenting (404)', async () => {
    setAccessToken(PARENT_TOKEN)
    stubApi({ state: 'gate-off' })

    renderRoute('/match/m1')

    expect(await screen.findByRole('heading', { name: /Torslanda/ })).toBeInTheDocument()
    await waitFor(() =>
      expect(screen.queryByRole('heading', { name: 'Kallelse' })).not.toBeInTheDocument(),
    )
  })
})

describe('tränaren kallar', () => {
  it('ser knappen och öppnar kallelsen', async () => {
    setAccessToken(coachToken('gul'))
    const sent = stubApi({
      token: coachToken('gul'),
      state: { callOpen: false, kickoffUtc: FUTURE, myResponse: null },
    })

    renderRoute('/match/m1')

    const button = await screen.findByRole('button', { name: 'Kalla till matchen' })
    await userEvent.click(button)

    await waitFor(() =>
      expect(
        sent.some(
          (r) => r.method === 'POST' && r.url.includes('/teams/gul/matches/m1/attendance/call'),
        ),
      ).toBe(true),
    )
  })

  it('en förälder ser en upplysning i stället för knappen innan tränaren kallat', async () => {
    setAccessToken(PARENT_TOKEN)
    stubApi({ state: { callOpen: false, kickoffUtc: FUTURE, myResponse: null } })

    renderRoute('/match/m1')

    expect(
      await screen.findByText('Tränaren har inte kallat till den här matchen än.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Kalla till matchen' })).not.toBeInTheDocument()
  })

  it('ser summeringen: antal och vilka vuxna som svarat', async () => {
    setAccessToken(coachToken('gul'))
    stubApi({
      token: coachToken('gul'),
      state: { callOpen: true, kickoffUtc: FUTURE, myResponse: null },
      summary: {
        comingPeople: 5,
        maybePeople: 1,
        cantComeFamilies: 2,
        respondedFamilies: 4,
        responders: [
          { id: 'a', name: 'Anna Berg', status: 'Coming', count: 3 },
          { id: 'b', name: 'Bengt Ek', status: 'CantCome', count: 0 },
        ],
      },
    })

    renderRoute('/match/m1')

    expect(await screen.findByRole('heading', { name: 'Svar hittills' })).toBeInTheDocument()
    // Exakt "5", inte /5/: matchens relativa dagsetikett ("Om 95 dagar") innehåller också
    // en femma, och det talet ändras med dagens datum. En delsträngsmatchning blir därför
    // en tidsinställd bomb — den föll den dag matchen råkade ligga 95 dagar bort.
    expect(screen.getByText('5', { exact: true })).toBeInTheDocument()
    expect(screen.getByText(/Anna Berg/)).toBeInTheDocument()
    expect(screen.getByText(/Bengt Ek/)).toBeInTheDocument()
  })

  it('ser vilka som inte svarat och kan påminna dem', async () => {
    setAccessToken(coachToken('gul'))
    const sent = stubApi({
      token: coachToken('gul'),
      state: { callOpen: true, kickoffUtc: FUTURE, myResponse: null },
      summary: {
        comingPeople: 3,
        maybePeople: 0,
        cantComeFamilies: 0,
        respondedFamilies: 1,
        responders: [{ id: 'a', name: 'Anna Berg', status: 'Coming', count: 3 }],
        notAnsweredCount: 2,
        notAnsweredNames: ['Bengt Ek', 'Cecilia Dahl'],
      },
    })

    renderRoute('/match/m1')

    expect(await screen.findByText(/2 har inte svarat/)).toBeInTheDocument()
    expect(screen.getByText(/Bengt Ek, Cecilia Dahl/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Påminn dem som inte svarat' }))

    await waitFor(() =>
      expect(sent.some((r) => r.method === 'POST' && r.url.includes('/attendance/remind'))).toBe(
        true,
      ),
    )
    expect(await screen.findByText('Påminde 2 föräldrar.')).toBeInTheDocument()
  })

  it('en förälder ser ingen summering', async () => {
    // Summeringen bar de svarande vuxnas namn -- lagets egen sak, bara for tranaren (§KM.1).
    setAccessToken(PARENT_TOKEN)
    stubApi({
      state: { callOpen: true, kickoffUtc: FUTURE, myResponse: null },
      summary: {
        comingPeople: 5,
        maybePeople: 0,
        cantComeFamilies: 0,
        respondedFamilies: 5,
        responders: [{ id: 'a', name: 'Anna Berg', status: 'Coming', count: 5 }],
      },
    })

    renderRoute('/match/m1')

    // Formuläret finns (föräldern kan svara), men summeringen och namnen gör det inte.
    expect(await screen.findByRole('button', { name: 'Svara' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Svar hittills' })).not.toBeInTheDocument()
    expect(screen.queryByText('Anna Berg')).not.toBeInTheDocument()
  })
})

describe('den vuxna svarar', () => {
  it('skickar status och antal', async () => {
    setAccessToken(PARENT_TOKEN)
    const sent = stubApi({ state: { callOpen: true, kickoffUtc: FUTURE, myResponse: null } })

    renderRoute('/match/m1')

    await userEvent.selectOptions(await screen.findByLabelText('Hur många kommer?'), '2')
    await userEvent.click(screen.getByRole('button', { name: 'Svara' }))

    await waitFor(() => {
      const put = sent.find((r) => r.method === 'PUT' && r.url.includes('/attendance/response'))
      expect(put?.body).toEqual({ status: 'Coming', count: 2 })
    })
  })

  it('visar det egna svaret och låter det ändras', async () => {
    setAccessToken(PARENT_TOKEN)
    stubApi({
      state: {
        callOpen: true,
        kickoffUtc: FUTURE,
        myResponse: { status: 'Coming', count: 2, updatedUtc: '2026-09-10T18:00:00Z' },
      },
    })

    renderRoute('/match/m1')

    expect(await screen.findByText(/Ditt svar:/)).toBeInTheDocument()
    expect(screen.getByText('Kommer (2)')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ändra svar' })).toBeInTheDocument()
  })

  it('frågar inte efter ett antal för "kan inte"', async () => {
    setAccessToken(PARENT_TOKEN)
    stubApi({ state: { callOpen: true, kickoffUtc: FUTURE, myResponse: null } })

    renderRoute('/match/m1')

    await userEvent.click(await screen.findByLabelText('Kan inte'))

    expect(screen.queryByLabelText('Hur många kommer?')).not.toBeInTheDocument()
  })

  it('går inte att svara på en match som spelats', async () => {
    setAccessToken(PARENT_TOKEN)
    stubApi({
      kickoffUtc: PAST,
      state: {
        callOpen: true,
        kickoffUtc: PAST,
        myResponse: { status: 'Coming', count: 1, updatedUtc: '2026-01-09T18:00:00Z' },
      },
    })

    renderRoute('/match/m1')

    expect(await screen.findByText(/Du svarade: Kommer \(1\)/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Svara|Ändra svar/ })).not.toBeInTheDocument()
  })
})
