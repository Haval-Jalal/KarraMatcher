import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Notisinställningen på Inställningar-sidan — en enda global på/av (`#332`-uppföljning).
 *
 * Vaktar att en inloggad förälder ser sin på/av och kan ändra den, och att en avstängning
 * sparas med en gång. Att en gäst inte når sidan sköter route-grinden (§KM.3).
 */

const TOKEN = `x.${btoa('{"email":"foralder@example.com"}')}.y`

const team = { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' }

function stubApi(settings: { enabled: boolean }) {
  const sent: { url: string; method: string; body: unknown }[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      const body: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null

      sent.push({ url, method, body })

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh'))
        return Promise.resolve(jsonResponse({ accessToken: TOKEN }))

      if (url.includes('/notification-settings')) {
        // PUT svarar med det som skickades; GET med utgångsläget.
        return Promise.resolve(jsonResponse(method === 'PUT' ? body : settings))
      }

      if (url.includes('/teams/gul/events')) {
        return Promise.resolve(jsonResponse({ team, events: [] }))
      }
      if (url.endsWith('/api/v1/teams')) return Promise.resolve(jsonResponse([team]))

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

describe('notisinställningar', () => {
  it('en inloggad ser sin globala notis-på/av', async () => {
    setAccessToken(TOKEN)
    stubApi({ enabled: true })

    renderRoute('/installningar')

    expect(await screen.findByRole('heading', { name: 'Notiser' })).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: /Push-notiser/ })).toBeChecked()
  })

  it('sparar en avstängning med en gång', async () => {
    setAccessToken(TOKEN)
    const sent = stubApi({ enabled: true })

    renderRoute('/installningar')

    await userEvent.click(await screen.findByRole('checkbox', { name: /Push-notiser/ }))

    await waitFor(() => {
      const put = sent.find((r) => r.method === 'PUT' && r.url.includes('/notification-settings'))
      expect(put?.body).toEqual({ enabled: false })
    })
  })
})
