import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'

import { useCupSummary } from '../useCup'

/**
 * Cup-sammanställningen måste vara färsk (#588): först-till-kvarn med hårt platstak, och
 * lagplaceringen läses härifrån. staleTime:0 gör att en ny observer hämtar om direkt, i stället
 * för att visa den globala 60s-cachens inaktuella "platser kvar".
 */

const EVENT = 'event-1'
const TOKEN = `x.${btoa(JSON.stringify({ email: 'a@example.com', sub: 'me' }))}.y`

function countingFetch(): { summaryCalls: () => number } {
  let summaryCalls = 0
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)
      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh'))
        return Promise.resolve(jsonResponse({ accessToken: TOKEN }))
      if (url.includes(`/events/${EVENT}/cup`)) {
        summaryCalls += 1
        return Promise.resolve(
          jsonResponse({
            open: true,
            capacity: 10,
            spotsTaken: 1,
            spotsLeft: 9,
            isFull: false,
            signedUp: [],
            mine: [],
            teams: [],
          }),
        )
      }
      return Promise.resolve(jsonResponse({}))
    }),
  )
  return { summaryCalls: () => summaryCalls }
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
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useCupSummary färskhet (#588)', () => {
  it('hämtar om vid ny observer trots appens 60s-standard (staleTime:0)', async () => {
    const spy = countingFetch()
    // Samma standard som appen (60s): utan hookens egen staleTime:0 skulle en remount INTE hämta om.
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, staleTime: 60_000, gcTime: Infinity } },
    })

    const first = renderHook(() => useCupSummary(EVENT), { wrapper: wrapper(client) })
    await waitFor(() => expect(first.result.current.data).toBeDefined())
    expect(spy.summaryCalls()).toBe(1)

    first.unmount()

    const second = renderHook(() => useCupSummary(EVENT), { wrapper: wrapper(client) })
    await waitFor(() => expect(second.result.current.data).toBeDefined())

    // staleTime:0 → den nya observern hämtar om direkt (med 60s hade den serverat cachen: 1).
    await waitFor(() => expect(spy.summaryCalls()).toBe(2))
  })
})
