import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { loginWithPasskey } from '@/features/passkeys'
import { clearSession } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

// Passkey-modulen mockas: WebAuthn-anropen går inte i jsdom. Här prövas att knappen visas när
// stödet finns och att den kör inloggningen respektive säger till vid fel.
vi.mock('@/features/passkeys', () => ({
  passkeysSupported: vi.fn(() => true),
  loginWithPasskey: vi.fn(),
  PasskeysSection: () => null,
}))

const mockLogin = vi.mocked(loginWithPasskey)

function stubAuth() {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) return Promise.resolve(jsonResponse({ title: 'Nej' }, 401))

      return Promise.resolve(jsonResponse({}))
    }),
  )
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  stubAuth()
})

afterEach(() => {
  vi.clearAllMocks()
  vi.unstubAllGlobals()
})

describe('inloggning med passkey', () => {
  it('kör passkey-inloggningen när man trycker på knappen', async () => {
    mockLogin.mockResolvedValue(undefined)
    const user = userEvent.setup()

    renderRoute('/logga-in')

    await user.click(await screen.findByRole('button', { name: 'Logga in med passkey' }))

    expect(mockLogin).toHaveBeenCalledOnce()
  })

  it('säger till på svenska om passkey-inloggningen inte gick', async () => {
    mockLogin.mockRejectedValue(new Error('avbruten'))
    const user = userEvent.setup()

    renderRoute('/logga-in')

    await user.click(await screen.findByRole('button', { name: 'Logga in med passkey' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/Passkey-inloggningen gick inte/)
  })
})
