import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Barnhantering (§KM.1, `#196`/`#283`) ur adminens perspektiv: Truppen listar alla barn och
 * Lag listar färgerna, man drar sig in i ett barn för att byta lag eller koppla en
 * vårdnadshavare, och en koppling utan samtycke möts av ett begripligt svenskt fel (§KM.6).
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

async function openTrupp(): Promise<ReturnType<typeof userEvent.setup>> {
  const user = userEvent.setup()
  renderRoute('/admin')
  await user.selectOptions(await screen.findByLabelText('Trupp'), 'trupp-1')
  // Barn & lag bor bakom sin flik sedan admin blev översikt + sektioner (#279), och är sedan
  // #283 en borra-in-vy: Truppen (alla barn) och Lag (färgerna), inte en utfälld roster.
  await user.click(await screen.findByRole('tab', { name: 'Barn & lag' }))
  return user
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('Barn & lag', () => {
  it('listar alla barn i Truppen och barnen per lag under Lag', async () => {
    stub(adminToken, (url) => {
      if (url.includes('/children')) return { teams: TEAMS, children: CHILDREN }
      return []
    })
    setAccessToken(adminToken)

    const user = await openTrupp()

    // Truppen: alla barn i en lista.
    expect(await screen.findByRole('button', { name: /Liam J/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Nora K/ })).toBeInTheDocument()

    // Lag → Gul: bara Liam (Nora saknar lag).
    await user.click(screen.getByRole('button', { name: 'Lag' }))
    await user.click(await screen.findByRole('button', { name: /Gul/ }))

    expect(await screen.findByRole('button', { name: /Liam J/ })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Nora K/ })).not.toBeInTheDocument()
  })

  it('lägger till ett barn — anropet bär förnamn, initial och lag', async () => {
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

    const user = await openTrupp()

    // Formuläret ligger bakom en knapp; öppna det och fyll i.
    await user.click(await screen.findByRole('button', { name: 'Lägg till barn' }))
    await user.type(screen.getByLabelText('Förnamn'), 'Ada')
    await user.type(screen.getByLabelText('Efternamnets initial'), 'S')
    await user.selectOptions(screen.getByLabelText('Lag (valfritt)'), 't1')
    await user.click(screen.getByRole('button', { name: 'Lägg till barn' }))

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

    // Kvitto efter sparat, och fältet tömt för nästa barn.
    expect(await screen.findByText('Ada S lades till.')).toBeInTheDocument()
    expect(screen.getByLabelText('Förnamn')).toHaveValue('')
  })

  it('visar ett begripligt fel när samtycke saknas (§KM.6)', async () => {
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

    const user = await openTrupp()

    // Öppna Liam och försök koppla en vårdnadshavare utan samtycke.
    await user.click(await screen.findByRole('button', { name: /Liam J/ }))
    await user.type(
      screen.getByLabelText('Koppla en vårdnadshavare (adress)'),
      'foralder@example.com',
    )
    await user.click(screen.getByRole('button', { name: 'Koppla vårdnadshavare' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Samtycke saknas')
  })
})
