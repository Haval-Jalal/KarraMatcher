import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Inställningar-sidan: samlar app-inställningar (i dag notiser), nådd via "Mer".
 *
 * Notiser är per lag, så sidan listar de lag du är med i — var och en med sin ruta.
 */

const TOKEN = `x.${btoa('{"email":"foralder@example.com"}')}.y`

function stub(teams: { slug: string; name: string; ageGroup: string; colorHex: string }[]) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: TOKEN }))
      }
      if (url.includes('/notification-settings')) {
        return Promise.resolve(
          jsonResponse({ eventChanges: true, kallelser: true, carpool: true, chat: true }),
        )
      }
      if (url.endsWith('/api/v1/teams')) return Promise.resolve(jsonResponse(teams))

      return Promise.resolve(jsonResponse({}))
    }),
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

describe('inställningar', () => {
  it('listar en ruta per lag man är med i', async () => {
    stub([
      { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' },
      { slug: 'bla', name: 'Blå', ageGroup: 'P2016', colorHex: '#1E3F8A' },
    ])

    renderRoute('/installningar')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Inställningar' }),
    ).toBeInTheDocument()
    expect(await screen.findByRole('heading', { name: 'P2016 Gul' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'P2016 Blå' })).toBeInTheDocument()
  })

  it('säger till när man inte är med i något lag', async () => {
    stub([])

    renderRoute('/installningar')

    await screen.findByRole('heading', { level: 1, name: 'Inställningar' })
    expect(await screen.findByText('Du är inte med i något lag än.')).toBeInTheDocument()
  })
})
