import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Inbjudningar (§KM.3, `#193`), ur användarens perspektiv: en förälder accepterar via länken
 * och blir medlem, och en trupp-admin bjuder in via sin vy.
 */

function tokenWith(claims: Record<string, unknown>): string {
  return `x.${btoa(JSON.stringify(claims))}.y`
}

const TOKEN = 'abc123token'

interface Sent {
  url: string
  method: string
  body: unknown
}

function stub(token: string, routes: (url: string, method: string) => unknown): Sent[] {
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
      // Tomt token = utloggad: förnyelsen mot cookien svarar 401 och gästen möts av
      // inloggningen (`#255`). Annars ger den access-token som en inloggad medlem.
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(
          token === ''
            ? jsonResponse({ title: 'Ingen session' }, 401)
            : jsonResponse({ accessToken: token }),
        )
      }

      return Promise.resolve(jsonResponse(routes(url, method)))
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

describe('Inbjudningens landningssida', () => {
  it('en inloggad med rätt adress kan gå med', async () => {
    const user = userEvent.setup()
    const token = tokenWith({ email: 'inbjuden@example.com' })
    const sent = stub(token, (url, method) => {
      if (url.includes('/accept') && method === 'POST') return { truppName: 'P2016' }
      if (url.includes('/api/v1/invitations/')) {
        return { valid: true, truppName: 'P2016', lagName: null, email: 'inbjuden@example.com' }
      }
      return {}
    })
    setAccessToken(token)

    renderRoute(`/inbjudan/${TOKEN}`)

    await user.click(await screen.findByRole('button', { name: 'Gå med' }))

    expect(await screen.findByRole('heading', { name: 'Du är med!' })).toBeInTheDocument()
    expect(
      sent.some(
        (r) => r.url.includes(`/api/v1/invitations/${TOKEN}/accept`) && r.method === 'POST',
      ),
    ).toBe(true)
  })

  it('en utloggad ombeds logga in', async () => {
    stub('', (url) => {
      if (url.includes('/api/v1/invitations/')) {
        return { valid: true, truppName: 'P2016', lagName: null, email: 'inbjuden@example.com' }
      }
      return {}
    })

    renderRoute(`/inbjudan/${TOKEN}`)

    expect(await screen.findByRole('link', { name: 'Logga in' })).toBeInTheDocument()
  })
})

describe('Trupp-adminvyn', () => {
  it('admin kan skicka en inbjudan, och anropet bär adressen', async () => {
    const user = userEvent.setup()
    const token = tokenWith({ email: 'admin@example.com', 'admin-trupp': 'trupp-1' })
    const sent = stub(token, (url, method) => {
      if (url.includes('/my-trupper')) {
        return [
          { id: 'trupp-1', clubName: 'Kärra', sportName: 'Fotboll', name: 'P2016', season: '2026' },
        ]
      }
      if (url.includes('/invitations') && method === 'POST') {
        return {
          invitation: {
            id: 'inv-1',
            email: 'ny@example.com',
            status: 'Pending',
            teamId: null,
            teamName: null,
            createdUtc: '2026-09-16T10:00:00Z',
            expiresUtc: '2026-09-30T10:00:00Z',
          },
          acceptUrl: 'http://localhost:5173/inbjudan/xyz',
        }
      }
      return []
    })
    setAccessToken(token)

    renderRoute('/admin')

    await user.selectOptions(await screen.findByLabelText('Trupp'), 'trupp-1')

    // Efter val landar man på Översikt; Inbjudningar bor bakom sin flik (#279).
    await user.click(await screen.findByRole('tab', { name: 'Inbjudningar' }))

    const panel = await screen.findByRole('heading', { name: 'Inbjudningar' })
    const section = panel.closest('.admin-subsection') as HTMLElement

    await user.type(
      within(section).getByLabelText('Bjud in en vårdnadshavare (adress)'),
      'ny@example.com',
    )
    await user.click(within(section).getByRole('button', { name: 'Skicka inbjudan' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/admin/trupper/trupp-1/invitations') &&
            r.method === 'POST' &&
            (r.body as { email: string }).email === 'ny@example.com',
        ),
      ).toBe(true)
    })
  })

  it('landar på översikt och växlar till en sektion (#279)', async () => {
    const user = userEvent.setup()
    const token = tokenWith({ email: 'admin@example.com', 'admin-trupp': 'trupp-1' })
    stub(token, (url) => {
      if (url.includes('/my-trupper')) {
        return [
          { id: 'trupp-1', clubName: 'Kärra', sportName: 'Fotboll', name: 'P2016', season: '2026' },
        ]
      }
      if (url.includes('/children')) {
        return {
          teams: [{ id: 'gul', name: 'Gul', colorHex: '#D9A21B' }],
          children: [
            {
              id: 'c1',
              firstName: 'Liam',
              lastInitial: 'J',
              displayName: 'Liam J',
              teamId: 'gul',
              teamName: 'Gul',
              guardians: [],
            },
            {
              id: 'c2',
              firstName: 'Nova',
              lastInitial: 'S',
              displayName: 'Nova S',
              teamId: 'gul',
              teamName: 'Gul',
              guardians: [],
            },
          ],
        }
      }
      if (url.includes('/applications')) {
        return [
          {
            id: 'a1',
            applicantName: 'Sara S',
            applicantEmail: 'sara@example.com',
            status: 'Pending',
            createdUtc: '2026-09-16T10:00:00Z',
          },
        ]
      }
      return []
    })
    setAccessToken(token)

    renderRoute('/admin')

    await user.selectOptions(await screen.findByLabelText('Trupp'), 'trupp-1')

    // Översikt är default: nyckeltal och en "Att göra"-rad som pekar på Ansökningar.
    expect(await screen.findByText('En förälder väntar på svar på sin ansökan')).toBeInTheDocument()
    expect(screen.getByText('barn')).toBeInTheDocument()

    // Växla till "Barn & lag": lag-panelen dyker upp och översikten försvinner.
    await user.click(screen.getByRole('tab', { name: 'Barn & lag' }))

    expect(await screen.findByRole('heading', { name: 'Lag' })).toBeInTheDocument()
    expect(screen.queryByText('En förälder väntar på svar på sin ansökan')).not.toBeInTheDocument()
  })
})
