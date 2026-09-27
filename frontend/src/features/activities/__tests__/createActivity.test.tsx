import { fireEvent, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { emptyResponse, jsonResponse } from '@/test/apiStub'
import { renderWithProviders } from '@/test/renderWithProviders'

import { CreateActivity } from '../CreateActivity'

/**
 * Admin skapar en aktivitet och väljer kallelse-målgrupp i samma steg (§KM.7, `#333`).
 *
 * Det som vaktas: målgruppen löser sig till rätt barn och rätt lag-märke — hela truppen, ett valt
 * lag, eller namngivna barn — och cup/övrigt skapar bara händelsen utan kallelse. Barnen visas
 * som "Liam J" (§KM.1); pushen bär aldrig namnet (vaktas på backend).
 */

const TRUPP = 'trupp-1'
const ADMIN_TOKEN = `x.${btoa(JSON.stringify({ email: 'admin@example.com', 'admin-trupp': TRUPP }))}.y`

const roster = {
  teams: [
    { id: 'team-gul', name: 'Gul', colorHex: '#D9A21B' },
    { id: 'team-bla', name: 'Blå', colorHex: '#1E3F8A' },
  ],
  children: [
    {
      id: 'c1',
      firstName: 'Liam',
      lastInitial: 'J',
      displayName: 'Liam J',
      teamId: 'team-gul',
      teamName: 'Gul',
      guardians: [],
    },
    {
      id: 'c2',
      firstName: 'Noah',
      lastInitial: 'K',
      displayName: 'Noah K',
      teamId: 'team-gul',
      teamName: 'Gul',
      guardians: [],
    },
    {
      id: 'c3',
      firstName: 'Ella',
      lastInitial: 'S',
      displayName: 'Ella S',
      teamId: 'team-bla',
      teamName: 'Blå',
      guardians: [],
    },
  ],
}

interface Sent {
  url: string
  method: string
  body: unknown
}

function stub(): Sent[] {
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

      // Kallelsen: PUT /admin/trupper/{id}/events/{eventId}/kallelse
      if (url.includes('/kallelse')) return Promise.resolve(emptyResponse(204))

      // Skapa händelsen: POST /admin/trupper/{id}/events
      if (url.includes('/events')) return Promise.resolve(jsonResponse({ id: 'ev-new' }, 201))

      // Truppens roster (barnväljaren): GET /admin/trupper/{id}/children
      if (url.includes('/children')) return Promise.resolve(jsonResponse(roster))

      // Klubbens hemmaplan (EventForm visar den vid "Hemma")
      if (url.includes('/club-venue')) {
        return Promise.resolve(
          jsonResponse({ configured: true, name: 'Karra IP', address: 'Idrottsvagen 1' }),
        )
      }

      return Promise.resolve(jsonResponse({}))
    }),
  )

  return sent
}

function setTime(): void {
  // datetime-local: skriv värdet direkt (userEvent.type är opålitligt för den typen).
  const input = screen.getByLabelText(/svensk tid/)
  fireEvent.change(input, { target: { value: '2026-12-20T11:00' } })
}

const created = (sent: Sent[]) =>
  sent.find((r) => r.method === 'POST' && r.url.includes('/events') && !r.url.includes('/kallelse'))
const kallelse = (sent: Sent[]) =>
  sent.find((r) => r.method === 'PUT' && r.url.includes('/kallelse'))

beforeEach(() => {
  localStorage.clear()
  clearSession()
  setAccessToken(ADMIN_TOKEN)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('skapa aktivitet med kallelse-målgrupp', () => {
  it('träning till hela truppen kallar alla barn och blir trupp-vid', async () => {
    const sent = stub()
    await renderWithProviders(<CreateActivity truppId={TRUPP} />)

    await userEvent.selectOptions(await screen.findByLabelText('Typ'), 'Training')

    // Träning → förval "Hela truppen".
    expect(await screen.findByLabelText('Hela truppen')).toBeChecked()

    setTime()
    await userEvent.type(screen.getByLabelText('Rubrik'), 'Gemensam träning')
    await userEvent.click(screen.getByRole('button', { name: 'Lägg till händelsen' }))

    await waitFor(() => expect(created(sent)).toBeDefined())

    expect((created(sent)!.body as { type: string }).type).toBe('Training')
    expect((created(sent)!.body as { teamId: string | null }).teamId).toBeNull()

    const call = kallelse(sent)
    expect(call).toBeDefined()
    expect((call!.body as { childIds: string[] }).childIds.sort()).toEqual(['c1', 'c2', 'c3'])
  })

  it('match till ett valt lag märker händelsen med laget och kallar dess barn', async () => {
    const sent = stub()
    await renderWithProviders(<CreateActivity truppId={TRUPP} />)

    // Match är förval → målgrupp "Valda lag".
    expect(await screen.findByLabelText('Valda lag')).toBeChecked()
    await userEvent.click(await screen.findByLabelText('Gul'))

    setTime()
    await userEvent.type(screen.getByLabelText('Motståndare'), 'Torslanda')
    await userEvent.click(screen.getByRole('button', { name: 'Lägg till händelsen' }))

    await waitFor(() => expect(created(sent)).toBeDefined())

    expect((created(sent)!.body as { teamId: string | null }).teamId).toBe('team-gul')
    expect((kallelse(sent)!.body as { childIds: string[] }).childIds.sort()).toEqual(['c1', 'c2'])
  })

  it('namngivna barn kallar bara de utvalda och blir trupp-vid', async () => {
    const sent = stub()
    await renderWithProviders(<CreateActivity truppId={TRUPP} />)

    await userEvent.click(await screen.findByLabelText('Namngivna barn'))
    await userEvent.click(await screen.findByLabelText('Ella S'))

    setTime()
    await userEvent.type(screen.getByLabelText('Motståndare'), 'Torslanda')
    await userEvent.click(screen.getByRole('button', { name: 'Lägg till händelsen' }))

    await waitFor(() => expect(created(sent)).toBeDefined())

    expect((created(sent)!.body as { teamId: string | null }).teamId).toBeNull()
    expect((kallelse(sent)!.body as { childIds: string[] }).childIds).toEqual(['c3'])
  })

  it('cup skapar bara händelsen — ingen kallelse', async () => {
    const sent = stub()
    await renderWithProviders(<CreateActivity truppId={TRUPP} />)

    await userEvent.selectOptions(await screen.findByLabelText('Typ'), 'Cup')
    expect(screen.queryByLabelText('Hela truppen')).not.toBeInTheDocument()

    setTime()
    await userEvent.type(screen.getByLabelText('Rubrik'), 'Kungälvscupen')
    await userEvent.click(screen.getByRole('button', { name: 'Lägg till händelsen' }))

    await waitFor(() => expect(created(sent)).toBeDefined())
    expect(kallelse(sent)).toBeUndefined()
  })
})
