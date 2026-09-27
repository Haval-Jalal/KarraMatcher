import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Inställningar-sidan: samlar app-inställningar (i dag notiser), nådd via "Mer".
 *
 * Notiser är numera en global på/av plus <b>en enda</b> enhets-växel (`#332`-uppföljning) — inte
 * längre en per lag, som gav samma enhet flera identiska notiser.
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
      if (url.includes('/notification-settings')) {
        return Promise.resolve(jsonResponse({ enabled: true }))
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
  it('visar den globala notis-på/av och en enda enhets-sektion', async () => {
    stub()

    renderRoute('/installningar')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Inställningar' }),
    ).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'Notiser' })).toBeInTheDocument()

    // Exakt en enhets-sektion, inte en per lag (`#332`-uppföljning).
    expect(screen.getAllByRole('heading', { name: 'På den här enheten' })).toHaveLength(1)
  })

  it('hämtar inte längre lag-listan för enhets-sektionen', async () => {
    const urls = stub()

    renderRoute('/installningar')

    await screen.findByRole('heading', { level: 1, name: 'Inställningar' })

    // Den gamla per-lag-listan är borta: sidan kallar inte /api/v1/teams.
    expect(urls.some((url) => url.endsWith('/api/v1/teams'))).toBe(false)
  })
})
