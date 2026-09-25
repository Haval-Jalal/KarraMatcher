import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Lag-chatten (§KM.1/§KM.10, `#202`) ur medlemmens perspektiv: läsa och skriva i lagets egna
 * kanal, anmäla, och att en ledare (meta.isLeader) ser schemaläggningen.
 */

function tokenWith(claims: Record<string, unknown>): string {
  return `x.${btoa(JSON.stringify(claims))}.y`
}

const MESSAGES = [
  {
    id: 'm1',
    authorAccountId: 'a1',
    authorName: 'Tor T',
    body: 'Vem tar med bollar?',
    publishedUtc: '2026-10-01T09:00:00Z',
    deleted: false,
  },
]

interface Sent {
  url: string
  method: string
  body: unknown
}

function stub(token: string, isLeader: boolean): Sent[] {
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
        return Promise.resolve(jsonResponse({ accessToken: token }))
      if (url.includes('/chat/meta')) {
        return Promise.resolve(jsonResponse({ truppId: 'trupp-1', isLeader }))
      }
      if (url.endsWith('/chat/channels')) {
        return Promise.resolve(
          jsonResponse([
            { kind: 'Trupp', teamId: null, slug: null, name: 'P2016 chatt', colorHex: null },
            {
              kind: 'Team',
              teamId: 't-gul',
              slug: 'gul',
              name: 'Lag Gul chatt',
              colorHex: '#D9A21B',
            },
          ]),
        )
      }
      if (url.includes('/report')) return Promise.resolve(jsonResponse({}))
      if (url.includes('/chat/scheduled')) return Promise.resolve(jsonResponse([]))
      if (url.includes('/chat/messages')) return Promise.resolve(jsonResponse(MESSAGES))
      return Promise.resolve(jsonResponse([]))
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

describe('lag-chatt', () => {
  it('en lagmedlem ser lagets meddelanden', async () => {
    const token = tokenWith({ email: 'm@example.com', sub: 'me' })
    setAccessToken(token)
    stub(token, false)

    renderRoute('/lag/gul/chatt')

    expect(await screen.findByText('Vem tar med bollar?')).toBeInTheDocument()
  })

  it('en medlem kan skriva — anropet går till lagets kanal', async () => {
    const user = userEvent.setup()
    const token = tokenWith({ email: 'm@example.com', sub: 'me' })
    setAccessToken(token)
    const sent = stub(token, false)

    renderRoute('/lag/gul/chatt')

    await user.type(await screen.findByLabelText('Skriv ett meddelande'), 'Jag tar bollar')
    await user.click(screen.getByRole('button', { name: 'Skicka' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/teams/gul/chat/messages') &&
            r.method === 'POST' &&
            (r.body as { body: string }).body === 'Jag tar bollar',
        ),
      ).toBe(true)
    })
  })

  it('anmälan går till lagets report-endpoint', async () => {
    const user = userEvent.setup()
    const token = tokenWith({ email: 'm@example.com', sub: 'me' })
    setAccessToken(token)
    const sent = stub(token, false)

    renderRoute('/lag/gul/chatt')

    const row = (await screen.findByText('Vem tar med bollar?')).closest('li') as HTMLElement
    await user.click(within(row).getByRole('button', { name: 'Anmäl' }))
    await user.type(within(row).getByLabelText(/Varför anmäler/), 'Fel kanal')
    await user.click(within(row).getByRole('button', { name: 'Skicka anmälan' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/teams/gul/chat/messages/m1/report') &&
            r.method === 'POST' &&
            (r.body as { reason: string }).reason === 'Fel kanal',
        ),
      ).toBe(true)
    })
  })

  it('en ledare ser schemaläggning', async () => {
    const token = tokenWith({ email: 'ledare@example.com', sub: 'lead' })
    setAccessToken(token)
    stub(token, true)

    renderRoute('/lag/gul/chatt')

    expect(await screen.findByLabelText(/Schemalägg/)).toBeInTheDocument()
  })

  it('en icke-ledare ser inte schemaläggning', async () => {
    const token = tokenWith({ email: 'm@example.com', sub: 'me' })
    setAccessToken(token)
    stub(token, false)

    renderRoute('/lag/gul/chatt')

    await screen.findByText('Vem tar med bollar?')
    expect(screen.queryByLabelText(/Schemalägg/)).not.toBeInTheDocument()
  })
})
