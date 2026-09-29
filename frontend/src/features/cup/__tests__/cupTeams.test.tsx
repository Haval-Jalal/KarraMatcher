import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Cup-lags-bygget på cupens händelsesida (`#335`). Admin bygger cup-lag av de anmälda och placerar
 * dem; en vårdnadshavare ser lagen i läsläge. Behörigheten avgörs av servern — knapparna speglar.
 */

const CUP_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'
const TRUPP = 'trupp-1'

const ADMIN_TOKEN = `x.${btoa(JSON.stringify({ email: 'admin@example.com', sub: 'a', 'admin-trupp': TRUPP }))}.y`
const GUARDIAN_TOKEN = `x.${btoa(JSON.stringify({ email: 'vh@example.com', sub: 'g' }))}.y`

const CUP_EVENT = {
  id: CUP_ID,
  type: 'Cup',
  kickoffUtc: new Date(Date.now() + 7 * 86_400_000).toISOString(),
  title: 'Sommarcup',
  opponent: null,
  isHome: null,
  status: 'Scheduled',
  address: 'Karra IP',
  venue: { name: 'Karra IP', address: 'Karra IP', latitude: 57.79, longitude: 11.94 },
}

const TEAM = { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' }

interface Sent {
  url: string
  method: string
}

function stub(token: string, summary: Record<string, unknown>): Sent[] {
  const sent: Sent[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      sent.push({ url, method })

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh'))
        return Promise.resolve(jsonResponse({ accessToken: token }))
      if (url.includes('/cup/teams')) return Promise.resolve(jsonResponse({ id: 'new-team' }, 201))
      if (url.includes(`/events/${CUP_ID}/cup`)) {
        return Promise.resolve(jsonResponse({ teams: [], ...summary }))
      }
      if (url.includes(`/api/v1/events/${CUP_ID}`)) {
        return Promise.resolve(jsonResponse({ event: CUP_EVENT, team: TEAM, truppId: TRUPP }))
      }
      return Promise.resolve(jsonResponse({}))
    }),
  )

  return sent
}

const openCup = {
  open: true,
  capacity: 10,
  spotsTaken: 1,
  spotsLeft: 9,
  isFull: false,
  signedUp: [{ childId: 'c1', displayName: 'Noah K', teamName: 'Gul', colorHex: '#D9A21B' }],
  mine: [],
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('cup-lag på händelsesidan', () => {
  it('en admin skapar ett cup-lag', async () => {
    const user = userEvent.setup()
    setAccessToken(ADMIN_TOKEN)
    const sent = stub(ADMIN_TOKEN, { ...openCup, teams: [] })

    renderRoute(`/handelse/${CUP_ID}`)

    await user.type(await screen.findByLabelText('Nytt cup-lag'), 'Lag 1')
    await user.click(screen.getByRole('button', { name: 'Skapa cup-lag' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.method === 'POST' &&
            r.url.includes(`/admin/trupper/${TRUPP}/events/${CUP_ID}/cup/teams`),
        ),
      ).toBe(true)
    })
  })

  it('en admin placerar ett anmält barn i ett lag', async () => {
    const user = userEvent.setup()
    setAccessToken(ADMIN_TOKEN)
    const sent = stub(ADMIN_TOKEN, {
      ...openCup,
      teams: [{ id: 't1', name: 'Lag 1', members: [] }],
    })

    renderRoute(`/handelse/${CUP_ID}`)

    await user.selectOptions(await screen.findByLabelText('Noah K'), 't1')

    await waitFor(() => {
      expect(
        sent.some((r) => r.method === 'PUT' && r.url.includes(`/cup/teams/t1/children/c1`)),
      ).toBe(true)
    })
  })

  it('en admin byter namn på ett cup-lag (#408)', async () => {
    const user = userEvent.setup()
    setAccessToken(ADMIN_TOKEN)
    const sent = stub(ADMIN_TOKEN, {
      ...openCup,
      teams: [{ id: 't1', name: 'Lag 1', members: [] }],
    })

    renderRoute(`/handelse/${CUP_ID}`)

    await user.click(await screen.findByRole('button', { name: 'Byt namn' }))
    const field = screen.getByLabelText('Nytt namn på Lag 1')
    await user.clear(field)
    await user.type(field, 'Lag Röd')
    await user.click(screen.getByRole('button', { name: 'Spara' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.method === 'PUT' &&
            r.url.includes(`/admin/trupper/${TRUPP}/events/${CUP_ID}/cup/teams/t1`) &&
            !r.url.includes('/children'),
        ),
      ).toBe(true)
    })
  })

  it('en vårdnadshavare ser cup-lagen i läsläge utan skapa-knapp', async () => {
    setAccessToken(GUARDIAN_TOKEN)
    stub(GUARDIAN_TOKEN, {
      ...openCup,
      teams: [{ id: 't1', name: 'Lag 1', members: [{ childId: 'c1', displayName: 'Noah K' }] }],
    })

    renderRoute(`/handelse/${CUP_ID}`)

    expect(await screen.findByText('Lag 1')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Skapa cup-lag' })).not.toBeInTheDocument()
  })
})
