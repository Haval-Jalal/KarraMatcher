import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Cupens öppna anmälan på händelsesidan (`#296`) ur en vårdnadshavares perspektiv: se platser
 * kvar, anmäla sitt barn, och se de anmälda. "Fullt" och behörighet avgörs av servern.
 */

const CUP_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'

function guardianToken(): string {
  return `x.${btoa(JSON.stringify({ email: 'vh@example.com', sub: 'me' }))}.y`
}

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

function stub(summary: unknown): Sent[] {
  const sent: Sent[] = []
  const token = guardianToken()

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      sent.push({ url, method })

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh'))
        return Promise.resolve(jsonResponse({ accessToken: token }))
      if (url.includes(`/events/${CUP_ID}/cup`)) return Promise.resolve(jsonResponse(summary))
      if (url.includes(`/api/v1/events/${CUP_ID}`)) {
        return Promise.resolve(jsonResponse({ event: CUP_EVENT, team: TEAM, truppId: 'trupp-1' }))
      }
      return Promise.resolve(jsonResponse({}))
    }),
  )

  return sent
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  setAccessToken(guardianToken())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('cup-anmälan på händelsesidan', () => {
  it('visar platser kvar, de anmälda, och låter en förälder anmäla sitt barn', async () => {
    const user = userEvent.setup()
    const sent = stub({
      open: true,
      capacity: 10,
      spotsTaken: 3,
      spotsLeft: 7,
      isFull: false,
      signedUp: [{ childId: 'c1', displayName: 'Noah K', teamName: 'Gul', colorHex: '#D9A21B' }],
      mine: [{ childId: 'mine1', displayName: 'Liam J', signedUp: false }],
    })

    renderRoute(`/handelse/${CUP_ID}`)

    // Läget och de anmälda syns.
    expect(await screen.findByText('7 av 10 platser kvar.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Anmälda (3)' })).toBeInTheDocument()
    expect(screen.getByText('Noah K')).toBeInTheDocument()

    // Föräldern anmäler sitt barn → POST till anmälnings-endpointen.
    await user.click(screen.getByRole('button', { name: 'Anmäl' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) => r.url.includes(`/events/${CUP_ID}/cup/children/mine1`) && r.method === 'POST',
        ),
      ).toBe(true)
    })
  })

  it('stänger av Anmäl-knappen när cupen är fullbokad', async () => {
    stub({
      open: true,
      capacity: 2,
      spotsTaken: 2,
      spotsLeft: 0,
      isFull: true,
      signedUp: [],
      mine: [{ childId: 'mine1', displayName: 'Liam J', signedUp: false }],
    })

    renderRoute(`/handelse/${CUP_ID}`)

    expect(await screen.findByText(/Fullt\./)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anmäl' })).toBeDisabled()
  })
})
