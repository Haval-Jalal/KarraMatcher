import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Inställningar-sidan: samlar app-inställningar (i dag notiser), nådd via "Mer".
 *
 * Notiser är numera <b>en enda växel per enhet</b> (`#332`-uppföljning) — ingen separat global
 * flagga, ingen per-lag-lista.
 */

const TOKEN = `x.${btoa('{"email":"foralder@example.com"}')}.y`

function stub(): string[] {
  const urls: string[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)
      urls.push(url)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: TOKEN }))
      }

      return Promise.resolve(jsonResponse({}))
    }),
  )

  return urls
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  setAccessToken(TOKEN)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('inställningar', () => {
  it('visar en enda notis-sektion', async () => {
    stub()

    renderRoute('/installningar')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Inställningar' }),
    ).toBeInTheDocument()

    // Exakt en notis-sektion — ingen separat global växel, ingen per-lag-lista.
    expect(await screen.findAllByRole('heading', { name: 'Notiser' })).toHaveLength(1)
    expect(screen.queryByRole('heading', { name: 'På den här enheten' })).not.toBeInTheDocument()
  })

  it('kallar varken lag-listan eller den gamla notis-endpointen', async () => {
    const urls = stub()

    renderRoute('/installningar')

    await screen.findByRole('heading', { level: 1, name: 'Inställningar' })

    expect(urls.some((url) => url.endsWith('/api/v1/teams'))).toBe(false)
    expect(urls.some((url) => url.includes('/notification-settings'))).toBe(false)
  })
})
