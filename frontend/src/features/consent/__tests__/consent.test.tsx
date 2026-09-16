import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Vårdnadshavarsamtycke på Mitt konto (§KM.6, `#195`): läs texten och samtyck, och se sedan
 * vad man samtyckte till.
 */

const TOKEN = `x.${btoa(JSON.stringify({ email: 'foralder@example.com' }))}.y`

const PROFILE = { firstName: 'Anna', lastName: null, displayName: 'Anna', needsName: false }
const CONSENT_TEXT = { version: '1', text: 'Samtyckestext om barnets uppgifter.' }

interface Sent {
  url: string
  method: string
  body: unknown
}

function stub(myConsent: () => unknown): Sent[] {
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
      if (url.includes('/auth/refresh'))
        return Promise.resolve(jsonResponse({ accessToken: TOKEN }))
      if (url.includes('/auth/profile')) return Promise.resolve(jsonResponse(PROFILE))
      if (url.includes('/consent/current')) return Promise.resolve(jsonResponse(CONSENT_TEXT))
      if (url.includes('/consent/me')) return Promise.resolve(jsonResponse(myConsent()))
      if (url.endsWith('/api/v1/consent') && method === 'POST') {
        return Promise.resolve(jsonResponse({}, 204))
      }

      return Promise.resolve(jsonResponse({}))
    }),
  )

  return sent
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('Samtycke på Mitt konto', () => {
  it('en förälder som inte samtyckt kan läsa texten och samtycka', async () => {
    const user = userEvent.setup()
    let consented = false
    const sent = stub(() =>
      consented
        ? {
            hasConsentedToCurrent: true,
            version: '1',
            grantedUtc: '2026-09-16T10:00:00Z',
            text: CONSENT_TEXT.text,
          }
        : { hasConsentedToCurrent: false, version: null, grantedUtc: null, text: null },
    )
    setAccessToken(TOKEN)

    renderRoute('/konto')

    const consent = await screen.findByRole('button', { name: 'Jag samtycker' })
    expect(screen.getByText('Samtyckestext om barnets uppgifter.')).toBeInTheDocument()

    consented = true
    await user.click(consent)

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.endsWith('/api/v1/consent') &&
            r.method === 'POST' &&
            (r.body as { version: string }).version === '1',
        ),
      ).toBe(true)
    })

    expect(await screen.findByText(/Du har samtyckt till version 1/)).toBeInTheDocument()
  })

  it('en förälder som samtyckt ser vad hen samtyckte till', async () => {
    stub(() => ({
      hasConsentedToCurrent: true,
      version: '1',
      grantedUtc: '2026-09-16T10:00:00Z',
      text: CONSENT_TEXT.text,
    }))
    setAccessToken(TOKEN)

    renderRoute('/konto')

    expect(await screen.findByText(/Du har samtyckt till version 1/)).toBeInTheDocument()
    expect(screen.getByText('Visa vad du samtyckte till')).toBeInTheDocument()
  })
})
