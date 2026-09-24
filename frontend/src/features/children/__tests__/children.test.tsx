import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Barnhantering (§KM.1, `#196`) ur adminens perspektiv: rostern grupperas per färg-lag,
 * ett barn läggs till med förnamn + initial, och en koppling utan samtycke möts av ett
 * begripligt svenskt fel (§KM.6).
 */

function tokenWith(claims: Record<string, unknown>): string {
  return `x.${btoa(JSON.stringify(claims))}.y`
}

interface Sent {
  url: string
  method: string
  body: unknown
}

const TEAMS = [{ id: 't1', name: 'Gul', colorHex: '#D9A21B' }]

const CHILDREN = [
  {
    id: 'c1',
    firstName: 'Liam',
    lastInitial: 'J',
    displayName: 'Liam J',
    teamId: 't1',
    teamName: 'Gul',
    guardians: [],
  },
  {
    id: 'c2',
    firstName: 'Nora',
    lastInitial: 'K',
    displayName: 'Nora K',
    teamId: null,
    teamName: null,
    guardians: [],
  },
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
      // En route kan svara med ett färdigt Response (för fel-statuskoder) eller en kropp.
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
  await user.selectOptions(await screen.findByLabelText('Välj trupp'), 'trupp-1')
}

function childrenPanel(): HTMLElement {
  return screen.getByRole('heading', { name: 'Barn' }).closest('.admin-subsection') as HTMLElement
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('Barn-roster', () => {
  it('grupperar barnen under sitt lag och under Otilldelade', async () => {
    stub(adminToken, (url) => {
      if (url.includes('/children')) return { teams: TEAMS, children: CHILDREN }
      return []
    })
    setAccessToken(adminToken)

    await openTrupp()

    const panel = await waitFor(() => childrenPanel())
    const gul = within(panel).getByRole('heading', { name: /Gul/ }).closest('.roster-group')!
    const otilldelade = within(panel)
      .getByRole('heading', { name: 'Otilldelade' })
      .closest('.roster-group')!

    expect(within(gul as HTMLElement).getByText('Liam J')).toBeInTheDocument()
    expect(within(otilldelade as HTMLElement).getByText('Nora K')).toBeInTheDocument()
  })

  it('lägger till ett barn — anropet bär förnamn, initial och lag', async () => {
    const user = userEvent.setup()
    const sent = stub(adminToken, (url, method) => {
      if (url.includes('/children') && method === 'POST') {
        return {
          id: 'c9',
          firstName: 'Ada',
          lastInitial: 'S',
          displayName: 'Ada S',
          teamId: 't1',
          teamName: 'Gul',
          guardians: [],
        }
      }
      if (url.includes('/children')) return { teams: TEAMS, children: [] }
      return []
    })
    setAccessToken(adminToken)

    await openTrupp()

    const panel = await waitFor(() => childrenPanel())
    await user.type(within(panel).getByLabelText('Förnamn'), 'Ada')
    await user.type(within(panel).getByLabelText('Efternamnets initial'), 'S')
    await user.selectOptions(within(panel).getByLabelText('Lag (valfritt)'), 't1')
    await user.click(within(panel).getByRole('button', { name: 'Lägg till barn' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/admin/trupper/trupp-1/children') &&
            r.method === 'POST' &&
            (r.body as { firstName: string }).firstName === 'Ada' &&
            (r.body as { lastInitial: string }).lastInitial === 'S' &&
            (r.body as { teamId: string }).teamId === 't1',
        ),
      ).toBe(true)
    })

    // Kvitto efter sparat, och fältet tömt för nästa barn (`#259`).
    expect(await within(panel).findByText('Ada S lades till.')).toBeInTheDocument()
    expect(within(panel).getByLabelText('Förnamn')).toHaveValue('')
  })

  it('visar ett begripligt fel när samtycke saknas (§KM.6)', async () => {
    const user = userEvent.setup()
    stub(adminToken, (url, method) => {
      if (url.includes('/guardians') && method === 'POST') {
        return jsonResponse(
          {
            title: 'Samtycke saknas',
            detail: 'Vårdnadshavaren måste godkänna samtyckestexten innan barnet kopplas (§KM.6).',
          },
          409,
        )
      }
      if (url.includes('/children')) return { teams: TEAMS, children: CHILDREN }
      return []
    })
    setAccessToken(adminToken)

    await openTrupp()

    const panel = await waitFor(() => childrenPanel())
    const liam = within(panel).getByText('Liam J').closest('.roster-child') as HTMLElement

    await user.type(
      within(liam).getByLabelText('Koppla vårdnadshavare (adress)'),
      'foralder@example.com',
    )
    await user.click(within(liam).getByRole('button', { name: 'Koppla vårdnadshavare' }))

    expect(await within(liam).findByRole('alert')).toHaveTextContent('Samtycke saknas')
  })
})
