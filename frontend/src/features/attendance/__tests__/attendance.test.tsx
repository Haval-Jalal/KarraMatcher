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

const team = { slug: 'svart', name: 'Svart', ageGroup: 'P2016', colorHex: '#161616' }

function eventDetail() {
  return {
    team,
    truppId: TRUPP,
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
  summary?: unknown
  roster?: unknown
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
      if (url.includes('/kallelse/children/')) return Promise.resolve(emptyResponse(204))

      // Adminens kallelse (GET summering / PUT urval): /admin/.../kallelse
      if (url.includes('/admin/') && url.includes('/kallelse')) {
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

      if (url.includes('/api/v1/events/')) return Promise.resolve(jsonResponse(eventDetail()))

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
  it('en gäst ser ingen kallelse', async () => {
    stub({ mine: { callOpen: true, kickoffUtc: FUTURE, children: [] } })

    renderRoute(`/handelse/${EVENT}`)

    expect(await screen.findByRole('heading', { name: /Torslanda/ })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Kallelse' })).not.toBeInTheDocument()
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
