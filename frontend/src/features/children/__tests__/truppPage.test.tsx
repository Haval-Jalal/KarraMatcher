import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Truppen på egen adress (`/trupp/$truppId`, `#redesign`). Samma roster som `/admin`-fliken, men
 * som en riktig route med bakåtknapp och delbar länk. Vyn göms för den som inte är admin för just
 * den här truppen — bekvämlighet, inte säkerhet; servern (AdminOfTrupp) är grinden (§KM.3).
 */

function tokenWith(claims: Record<string, unknown>): string {
  return `x.${btoa(JSON.stringify(claims))}.y`
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
]

function stub(token: string) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: token }))
      }
      if (url.includes('/trupper/mina')) {
        return Promise.resolve(
          jsonResponse([
            { id: 'trupp-1', clubName: 'Kärra', name: 'P2016', season: '2026', isLeader: true },
          ]),
        )
      }
      if (url.includes('/children')) {
        return Promise.resolve(jsonResponse({ teams: TEAMS, children: CHILDREN }))
      }

      return Promise.resolve(jsonResponse([]))
    }),
  )
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('TruppPage', () => {
  it('visar truppens roster för en admin för truppen', async () => {
    const token = tokenWith({ email: 'admin@example.com', 'admin-trupp': 'trupp-1' })
    stub(token)
    setAccessToken(token)

    renderRoute('/trupp/trupp-1')

    expect(await screen.findByRole('heading', { level: 1, name: 'Truppen' })).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: /Liam J/ })).toBeInTheDocument()
  })

  it('flyttar fokus till rubriken vid borra-in och till fliken vid tillbaka (#482)', async () => {
    const token = tokenWith({ email: 'admin@example.com', 'admin-trupp': 'trupp-1' })
    stub(token)
    setAccessToken(token)
    const user = userEvent.setup()

    renderRoute('/trupp/trupp-1')

    // Borra in i ett barn → fokus ska landa på barnets rubrik, inte falla till <body>.
    await user.click(await screen.findByRole('button', { name: /Liam J/ }))
    const heading = await screen.findByRole('heading', { level: 3, name: 'Liam J' })
    await waitFor(() => expect(heading).toHaveFocus())

    // Tillbaka → fokus ska landa på Truppen-fliken, inte på <body>.
    await user.click(screen.getByRole('button', { name: '‹ Tillbaka' }))
    await waitFor(() => expect(screen.getByRole('button', { name: 'Truppen' })).toHaveFocus())
  })

  it('fokuserar första fältet när "Lägg till barn" fälls in (#484)', async () => {
    const token = tokenWith({ email: 'admin@example.com', 'admin-trupp': 'trupp-1' })
    stub(token)
    setAccessToken(token)
    const user = userEvent.setup()

    renderRoute('/trupp/trupp-1')

    await user.click(await screen.findByRole('button', { name: 'Lägg till barn' }))

    await waitFor(() => expect(screen.getByLabelText('Förnamn')).toHaveFocus())
  })

  it('göms för den som inte är admin för truppen', async () => {
    const token = tokenWith({ email: 'foralder@example.com' })
    stub(token)
    setAccessToken(token)

    renderRoute('/trupp/trupp-1')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Ingen behörighet' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Truppen' })).not.toBeInTheDocument()
  })
})
