import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Tränartillsättning (§KM.3, `#197`) ur adminens perspektiv: lagen listas med sina tränare,
 * en tränare tillsätts via adress, och ett saknat konto möts av ett begripligt svenskt fel.
 */

function tokenWith(claims: Record<string, unknown>): string {
  return `x.${btoa(JSON.stringify(claims))}.y`
}

interface Sent {
  url: string
  method: string
  body: unknown
}

const TEAMS = [
  {
    teamId: 't1',
    teamName: 'Gul',
    colorHex: '#D9A21B',
    coaches: [
      {
        accountId: 'a1',
        displayName: 'Tor T',
        email: 'tor@example.com',
        grantedUtc: '2026-09-18T10:00:00Z',
      },
    ],
  },
  { teamId: 't2', teamName: 'Blå', colorHex: '#1E3F8A', coaches: [] },
]

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
      if (url.includes('/auth/refresh'))
        return Promise.resolve(jsonResponse({ accessToken: token }))
      if (url.includes('/my-trupper')) {
        return Promise.resolve(
          jsonResponse([
            {
              id: 'trupp-1',
              clubName: 'Kärra',
              sportName: 'Fotboll',
              name: 'P2016',
              season: '2026',
            },
          ]),
        )
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

const adminToken = tokenWith({ email: 'admin@example.com', 'admin-trupp': 'trupp-1' })

async function openTrupp(): Promise<void> {
  const user = userEvent.setup()
  renderRoute('/admin')
  await user.selectOptions(await screen.findByLabelText('Trupp'), 'trupp-1')
  // Tränare bor bakom sin flik sedan admin blev översikt + sektioner (#279).
  await user.click(await screen.findByRole('tab', { name: 'Tränare' }))
}

function coachesPanel(): HTMLElement {
  return screen
    .getByRole('heading', { name: 'Tränare' })
    .closest('.admin-subsection') as HTMLElement
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('Tränartillsättning', () => {
  it('listar lagen med sina tränare', async () => {
    stub(adminToken, (url) => {
      if (url.includes('/coaches')) return { teams: TEAMS }
      return []
    })
    setAccessToken(adminToken)

    await openTrupp()

    const panel = await waitFor(() => coachesPanel())
    const gul = within(panel).getByRole('heading', { name: /Gul/ }).closest('.roster-group')!
    const bla = within(panel).getByRole('heading', { name: /Blå/ }).closest('.roster-group')!

    expect(within(gul as HTMLElement).getByText('Tor T')).toBeInTheDocument()
    expect(within(bla as HTMLElement).getByText('Ingen tränare än.')).toBeInTheDocument()
  })

  it('tillsätter en tränare — anropet bär lagets id och adressen', async () => {
    const user = userEvent.setup()
    const sent = stub(adminToken, (url, method) => {
      if (url.includes('/coaches') && method === 'POST') {
        return {
          accountId: 'a9',
          displayName: null,
          email: 'ny@example.com',
          grantedUtc: '2026-09-18T10:00:00Z',
        }
      }
      if (url.includes('/coaches')) return { teams: TEAMS }
      return []
    })
    setAccessToken(adminToken)

    await openTrupp()

    const panel = await waitFor(() => coachesPanel())
    const bla = within(panel)
      .getByRole('heading', { name: /Blå/ })
      .closest('.roster-group') as HTMLElement

    await user.type(within(bla).getByLabelText(/Tillsätt tränare i Blå/), 'ny@example.com')
    await user.click(within(bla).getByRole('button', { name: 'Tillsätt tränare' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/admin/trupper/trupp-1/teams/t2/coaches') &&
            r.method === 'POST' &&
            (r.body as { email: string }).email === 'ny@example.com',
        ),
      ).toBe(true)
    })
  })

  it('visar ett begripligt fel när kontot inte finns', async () => {
    const user = userEvent.setup()
    stub(adminToken, (url, method) => {
      if (url.includes('/coaches') && method === 'POST') {
        return jsonResponse(
          { title: 'Referens saknas', detail: 'Kontrollera att kontot finns.' },
          400,
        )
      }
      if (url.includes('/coaches')) return { teams: TEAMS }
      return []
    })
    setAccessToken(adminToken)

    await openTrupp()

    const panel = await waitFor(() => coachesPanel())
    const gul = within(panel)
      .getByRole('heading', { name: /Gul/ })
      .closest('.roster-group') as HTMLElement

    await user.type(within(gul).getByLabelText(/Tillsätt tränare i Gul/), 'finns-inte@example.com')
    await user.click(within(gul).getByRole('button', { name: 'Tillsätt tränare' }))

    expect(await within(gul).findByRole('alert')).toHaveTextContent('Referens saknas')
  })
})
