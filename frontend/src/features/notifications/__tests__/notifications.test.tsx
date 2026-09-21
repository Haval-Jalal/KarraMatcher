import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Notisinställningarna på lagsidan (`#65`).
 *
 * Vaktar att en inloggad förälder ser sina val och kan ändra dem, att en avstängning sparas
 * med en gång, och att en gäst inte ser något att ställa in.
 */

const TOKEN = `x.${btoa('{"email":"foralder@example.com"}')}.y`

const team = { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' }

function stubApi(settings: {
  eventChanges: boolean
  kallelser: boolean
  carpool: boolean
  chat: boolean
}) {
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
  it('en gäst ser inga inställningar', async () => {
    stubApi({ eventChanges: true, kallelser: true, carpool: true, chat: true })

    renderRoute('/lag/gul')

    // Lagsidan laddar, men notisrutan kräver konto.
    await waitFor(() =>
      expect(screen.queryByRole('heading', { name: 'Notiser' })).not.toBeInTheDocument(),
    )
  })

  it('en inloggad ser sina val', async () => {
    setAccessToken(TOKEN)
    stubApi({ eventChanges: true, kallelser: true, carpool: false, chat: true })

    renderRoute('/lag/gul')

    expect(await screen.findByRole('heading', { name: 'Notiser' })).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: /Händelser/ })).toBeChecked()
    expect(screen.getByRole('checkbox', { name: /Samåkning/ })).not.toBeChecked()
  })

  it('sparar en avstängning med en gång', async () => {
    setAccessToken(TOKEN)
    const sent = stubApi({ eventChanges: true, kallelser: true, carpool: true, chat: true })

    renderRoute('/lag/gul')

    await userEvent.click(await screen.findByRole('checkbox', { name: /Samåkning/ }))

    await waitFor(() => {
      const put = sent.find((r) => r.method === 'PUT' && r.url.includes('/notification-settings'))
      expect(put?.body).toEqual({
        eventChanges: true,
        kallelser: true,
        carpool: false,
        chat: true,
      })
    })
  })
})
