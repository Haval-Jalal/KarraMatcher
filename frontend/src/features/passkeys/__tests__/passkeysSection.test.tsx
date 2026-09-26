import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { PasskeysSection } from '@/features/passkeys'
import { passkeysSupported, registerPasskey } from '@/features/passkeys/webauthn'
import { setAccessToken, clearSession } from '@/lib/session'
import { emptyResponse, jsonResponse } from '@/test/apiStub'
import { renderWithProviders } from '@/test/renderWithProviders'

// Själva WebAuthn-anropen (navigator.credentials) går inte att köra i jsdom — de mockas, så
// testet prövar vyn och att den anropar rätt sak.
vi.mock('@/features/passkeys/webauthn', () => ({
  passkeysSupported: vi.fn(() => true),
  registerPasskey: vi.fn(() => Promise.resolve()),
  loginWithPasskey: vi.fn(),
}))

const mockSupported = vi.mocked(passkeysSupported)
const mockRegister = vi.mocked(registerPasskey)

function stub() {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/api/v1/passkeys')) {
        return Promise.resolve(
          jsonResponse([
            {
              id: 'p1',
              deviceLabel: 'iPhone',
              createdUtc: '2026-09-20T12:00:00Z',
              lastUsedUtc: null,
            },
          ]),
        )
      }

      return Promise.resolve(emptyResponse(204))
    }),
  )
}

beforeEach(() => {
  clearSession()
  setAccessToken('test-token')
  mockSupported.mockReturnValue(true)
  stub()
})

afterEach(() => {
  vi.clearAllMocks()
  vi.unstubAllGlobals()
})

describe('passkey-sektionen', () => {
  it('listar kontots passkeys och erbjuder att lägga till', async () => {
    await renderWithProviders(<PasskeysSection />)

    expect(await screen.findByText(/iPhone/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Lägg till passkey' })).toBeInTheDocument()
  })

  it('lägger till en passkey', async () => {
    const user = userEvent.setup()
    await renderWithProviders(<PasskeysSection />)

    await user.click(await screen.findByRole('button', { name: 'Lägg till passkey' }))

    expect(mockRegister).toHaveBeenCalledOnce()
  })

  it('säger till när webbläsaren inte stöder passkeys', async () => {
    mockSupported.mockReturnValue(false)

    await renderWithProviders(<PasskeysSection />)

    expect(await screen.findByText(/stöder inte passkeys/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Lägg till passkey' })).not.toBeInTheDocument()
  })
})
