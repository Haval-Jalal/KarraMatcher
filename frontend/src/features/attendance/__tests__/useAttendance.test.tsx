import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'

import { useKallelseSummary, useTeamKallelseSummary } from '../useAttendance'

/**
 * Admin- och tränar-summeringarna hämtar från olika endpoints och måste ha åtskilda cache-nycklar
 * (#554). Delade de nyckel skulle en användare som är både trupp-admin och tränare för händelsens
 * lag få en endpoints svar serverat till båda observers.
 */

const EVENT = 'event-1'
const TRUPP = 'trupp-1'
const SLUG = 'gul'
const TOKEN = `x.${btoa(JSON.stringify({ email: 'a@example.com', sub: 'me' }))}.y`

function stub(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)
      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh'))
        return Promise.resolve(jsonResponse({ accessToken: TOKEN }))
      // Två olika endpoints för samma händelse, med avsiktligt olika svar så en delad cache
      // skulle avslöja sig.
      if (url.includes('/admin/') && url.includes('/kallelse')) {
        return Promise.resolve(
          jsonResponse({ callOpen: true, coming: 1, notComing: 0, notAnswered: 0, children: [] }),
        )
      }
      if (url.includes('/teams/') && url.includes('/kallelse')) {
        return Promise.resolve(
          jsonResponse({ callOpen: true, coming: 2, notComing: 0, notAnswered: 0, children: [] }),
        )
      }
      return Promise.resolve(jsonResponse({}))
    }),
  )
}

function wrapper(client: QueryClient) {
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  setAccessToken(TOKEN)
  stub()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('kallelse-summeringarna delar inte cache (#554)', () => {
  it('admin- och tränar-vyn får var sitt endpoint-svar för samma händelse', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })

    const admin = renderHook(() => useKallelseSummary(TRUPP, EVENT, true), {
      wrapper: wrapper(client),
    })
    const team = renderHook(() => useTeamKallelseSummary(SLUG, EVENT, true), {
      wrapper: wrapper(client),
    })

    await waitFor(() => expect(admin.result.current.data).toBeDefined())
    await waitFor(() => expect(team.result.current.data).toBeDefined())

    // Delade de cache-nyckel skulle båda fått samma (en fetchers) svar.
    expect(admin.result.current.data?.coming).toBe(1)
    expect(team.result.current.data?.coming).toBe(2)
  })
})
