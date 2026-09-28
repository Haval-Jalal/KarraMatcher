import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { jsonResponse } from '@/test/apiStub'
import { renderWithProviders } from '@/test/renderWithProviders'

import { CoachKallelse } from '../CoachKallelse'

/**
 * Färg-lag-tränarens kallelse-panel (`#redesign`, §KM.7): tränaren väljer barn ur hela truppen
 * (eget lag + fyll-på ur andra lag) och skickar mot lagets endpoint.
 */

interface Sent {
  url: string
  method: string
  body: unknown
}

afterEach(() => {
  vi.unstubAllGlobals()
})

function stub(): Sent[] {
  const sent: Sent[] = []
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
      if (url.includes('/kallelse-roster')) {
        return Promise.resolve(
          jsonResponse({
            teams: [{ id: 't1', name: 'Gul', colorHex: '#D9A21B' }],
            children: [
              { id: 'c1', displayName: 'Liam J', teamId: 't1' },
              { id: 'c2', displayName: 'Nora K', teamId: null },
            ],
          }),
        )
      }
      // Coach summary/set/remind share the team kallelse URL — split on method.
      if (url.includes('/teams/gul/events/e1/kallelse')) {
        if (method === 'GET') {
          return Promise.resolve(
            jsonResponse({
              callOpen: false,
              coming: 0,
              notComing: 0,
              notAnswered: 0,
              children: [],
            }),
          )
        }
        return Promise.resolve(jsonResponse([]))
      }

      return Promise.resolve(jsonResponse([]))
    }),
  )
  return sent
}

describe('CoachKallelse', () => {
  it('en tränare väljer barn och skickar kallelse mot lagets endpoint', async () => {
    const sent = stub()
    const user = userEvent.setup()

    await renderWithProviders(<CoachKallelse slug="gul" eventId="e1" teamName="Gul" />)

    // Väljaren visar hela truppen (Gul + Otilldelade).
    await user.click(await screen.findByRole('checkbox', { name: /Liam J/ }))
    await user.click(screen.getByRole('button', { name: 'Skicka kallelse' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/teams/gul/events/e1/kallelse') &&
            r.method === 'PUT' &&
            (r.body as { childIds: string[] }).childIds.includes('c1'),
        ),
      ).toBe(true)
    })
  })
})
