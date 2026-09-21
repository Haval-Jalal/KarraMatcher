import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Trupp-chatten (§KM.1/§KM.10, `#201`) ur medlemmens perspektiv: läsa och skriva, ett
 * borttaget meddelande visas som "[borttaget]", anmäla, ledare schemalägger, admin ser
 * anmälningar.
 */

function tokenWith(claims: Record<string, unknown>): string {
  return `x.${btoa(JSON.stringify(claims))}.y`
}

const TRUPP = { id: 'trupp-1', clubName: 'Kärra', name: 'P2016', season: '2026' }

const MESSAGES = [
  {
    id: 'm1',
    authorAccountId: 'a1',
    authorName: 'Tor T',
    body: 'Hej alla',
    publishedUtc: '2026-10-01T09:00:00Z',
    deleted: false,
  },
  {
    id: 'm2',
    authorAccountId: 'a2',
    authorName: 'Nina',
    body: '',
    publishedUtc: '2026-10-01T10:00:00Z',
    deleted: true,
  },
]

interface Sent {
  url: string
  method: string
  body: unknown
}

function stub(
  token: string,
  isLeader: boolean,
  routes: (url: string, method: string) => unknown,
): Sent[] {
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
      if (url.endsWith('/api/v1/trupper/mina')) {
        return Promise.resolve(jsonResponse([{ ...TRUPP, isLeader }]))
      }

      const result = routes(url, method)
      if (result !== null && typeof result === 'object' && 'ok' in result) {
        return Promise.resolve(result as Response)
      }
      return Promise.resolve(jsonResponse(result))
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

describe('trupp-chatt', () => {
  it('en medlem ser meddelanden och ett borttaget visas som [borttaget]', async () => {
    setAccessToken(tokenWith({ email: 'm@example.com', sub: 'me' }))
    stub(tokenWith({ email: 'm@example.com', sub: 'me' }), false, (url) => {
      if (url.includes('/chat/messages')) return MESSAGES
      return []
    })

    renderRoute('/chatt/trupp-1')

    expect(await screen.findByText('Hej alla')).toBeInTheDocument()
    expect(screen.getByText('[borttaget]')).toBeInTheDocument()
  })

  it('en medlem kan skriva ett meddelande — anropet bär texten', async () => {
    const user = userEvent.setup()
    const token = tokenWith({ email: 'm@example.com', sub: 'me' })
    setAccessToken(token)
    const sent = stub(token, false, (url) => {
      if (url.includes('/chat/messages')) return MESSAGES
      return []
    })

    renderRoute('/chatt/trupp-1')

    await user.type(await screen.findByLabelText('Skriv ett meddelande'), 'Nytt inlägg')
    await user.click(screen.getByRole('button', { name: 'Skicka' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/trupper/trupp-1/chat/messages') &&
            r.method === 'POST' &&
            (r.body as { body: string }).body === 'Nytt inlägg' &&
            (r.body as { publishAt?: string }).publishAt === undefined,
        ),
      ).toBe(true)
    })
  })

  it('anmälan skickar en POST till report-endpointen', async () => {
    const user = userEvent.setup()
    const token = tokenWith({ email: 'm@example.com', sub: 'me' })
    setAccessToken(token)
    const sent = stub(token, false, (url) => {
      if (url.includes('/chat/messages')) return MESSAGES
      return []
    })

    renderRoute('/chatt/trupp-1')

    const row = (await screen.findByText('Hej alla')).closest('li') as HTMLElement
    await user.click(within(row).getByRole('button', { name: 'Anmäl' }))

    await waitFor(() => {
      expect(
        sent.some((r) => r.url.includes('/chat/messages/m1/report') && r.method === 'POST'),
      ).toBe(true)
    })
  })

  it('en ledare kan schemalägga — anropet bär en tid', async () => {
    const user = userEvent.setup()
    const token = tokenWith({ email: 'ledare@example.com', sub: 'lead' })
    setAccessToken(token)
    const sent = stub(token, true, (url) => {
      if (url.includes('/chat/scheduled')) return []
      if (url.includes('/chat/messages')) return MESSAGES
      return []
    })

    renderRoute('/chatt/trupp-1')

    await user.type(await screen.findByLabelText('Skriv ett meddelande'), 'Kom ihåg matchen')
    await user.click(screen.getByLabelText(/Schemalägg/))
    fireEvent.change(screen.getByLabelText('Skicka vid'), {
      target: { value: '2030-01-01T12:00' },
    })
    await user.click(screen.getByRole('button', { name: 'Schemalägg' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/trupper/trupp-1/chat/messages') &&
            r.method === 'POST' &&
            typeof (r.body as { publishAt?: string }).publishAt === 'string',
        ),
      ).toBe(true)
    })
  })

  it('en admin ser anmälda meddelanden', async () => {
    const token = tokenWith({ email: 'admin@example.com', sub: 'adm', 'admin-trupp': 'trupp-1' })
    setAccessToken(token)
    stub(token, true, (url) => {
      if (url.includes('/chat/reports')) {
        return [
          {
            messageId: 'm1',
            authorAccountId: 'a1',
            authorName: 'Tor T',
            body: 'Hej alla',
            deleted: false,
            reportCount: 2,
          },
        ]
      }
      if (url.includes('/chat/scheduled')) return []
      if (url.includes('/chat/messages')) return MESSAGES
      return []
    })

    renderRoute('/chatt/trupp-1')

    expect(await screen.findByRole('heading', { name: 'Anmälda meddelanden' })).toBeInTheDocument()
    expect(screen.getByText(/2 anmälningar/)).toBeInTheDocument()
  })
})
