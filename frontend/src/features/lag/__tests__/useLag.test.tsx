import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'

import { useCreateLag, useUpdateLag } from '../useLag'

/**
 * Ett nytt lag eller ett namn-/färgbyte måste slå igenom i de långlivade vyer som bär lagets namn
 * och färg — lagväljaren/temafärgen (`['teams']`, staleTime 1h), trupp-rostern och lag-rostern, och
 * lag-schemats rubrik. Annars står de kvar inaktuella i upp till en timme (#536).
 */

const TRUPP = 'trupp-1'
const TOKEN = `x.${btoa(JSON.stringify({ email: 'admin@example.com', 'admin-trupp': TRUPP }))}.y`

function stub(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)
      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      // Både skapa (POST) och ändra (PUT) svarar med laget.
      if (url.includes('/lag')) {
        return Promise.resolve(
          jsonResponse({
            id: 'lag-1',
            truppId: TRUPP,
            name: 'Gul',
            colorHex: '#D9A21B',
            slug: 'gul',
          }),
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

/** De vyer en lag-ändring måste röra utöver admin-lag-listan (#536). */
const EXPECTED_KEYS = [
  ['admin', 'lag', TRUPP],
  ['teams'],
  ['admin', 'roster', TRUPP],
  ['team', 'roster'],
  ['team-events'],
]

beforeEach(() => {
  localStorage.clear()
  clearSession()
  setAccessToken(TOKEN)
  stub()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('lag-muteringar invaliderar alla vyer som bär lagets namn/färg (#536)', () => {
  it('useCreateLag invaliderar lagväljare, rosters och lag-schema', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
    const invalidate = vi.spyOn(client, 'invalidateQueries')

    const { result } = renderHook(() => useCreateLag(TRUPP), { wrapper: wrapper(client) })

    result.current.mutate({ name: 'Gul', colorHex: '#D9A21B', slug: 'gul' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    for (const queryKey of EXPECTED_KEYS) {
      expect(invalidate).toHaveBeenCalledWith({ queryKey })
    }
  })

  it('useUpdateLag invaliderar lagväljare, rosters och lag-schema', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
    const invalidate = vi.spyOn(client, 'invalidateQueries')

    const { result } = renderHook(() => useUpdateLag(TRUPP), { wrapper: wrapper(client) })

    result.current.mutate({ id: 'lag-1', name: 'Grön', colorHex: '#12905a' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    for (const queryKey of EXPECTED_KEYS) {
      expect(invalidate).toHaveBeenCalledWith({ queryKey })
    }
  })
})
