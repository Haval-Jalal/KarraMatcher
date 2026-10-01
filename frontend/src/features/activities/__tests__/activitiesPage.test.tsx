import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Aktivitet-fliken (`#334`): truppens alla aktiviteter för en medlem, med en skapa-ingång som
 * bara en admin ser. Navbaren är stabil för alla roller (`#253`) — knappen är bekvämlighet, inte
 * en säkerhetsgräns (§KM.3).
 */

const TRUPP = 'trupp-1'
const PARENT_TOKEN = `x.${btoa('{"email":"foralder@example.com"}')}.y`
const ADMIN_TOKEN = `x.${btoa(JSON.stringify({ email: 'admin@example.com', 'admin-trupp': TRUPP }))}.y`

const FUTURE = '2027-06-01T10:00:00Z'

interface Options {
  token: string
  activities?: unknown
}

function stub(options: Options) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: options.token }))
      }

      // Medlemmens trupper (trupp-väljaren).
      if (url.includes('/api/v1/trupper/mina')) {
        return Promise.resolve(
          jsonResponse([{ id: TRUPP, clubName: 'Kärra', name: 'P2016', season: '2026' }]),
        )
      }

      // Truppens aktivitetslista.
      if (url.includes(`/api/v1/trupper/${TRUPP}/events`)) {
        return Promise.resolve(
          jsonResponse(
            options.activities ?? [
              {
                event: {
                  id: 'm1',
                  type: 'Match',
                  kickoffUtc: FUTURE,
                  title: null,
                  opponent: 'Torslanda',
                  isHome: false,
                  status: 'Scheduled',
                  address: 'Klarebergsvallen',
                  venue: {
                    name: 'Klarebergsvallen',
                    address: 'Klarebergsvallen',
                    latitude: 57.8,
                    longitude: 12,
                  },
                },
                team: { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' },
              },
            ],
          ),
        )
      }

      if (url.includes('/api/v1/hem')) {
        return Promise.resolve(
          jsonResponse({ nextEvent: null, pendingKallelser: [], latestChat: null }),
        )
      }

      return Promise.resolve(jsonResponse({}))
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

describe('Aktivitet-fliken', () => {
  it('en förälder ser läslistan men ingen skapa-knapp', async () => {
    setAccessToken(PARENT_TOKEN)
    stub({ token: PARENT_TOKEN })

    renderRoute('/aktivitet')

    expect(await screen.findByText(/Torslanda/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Skapa aktivitet' })).not.toBeInTheDocument()
  })

  it('en admin ser en skapa-ingång', async () => {
    setAccessToken(ADMIN_TOKEN)
    stub({ token: ADMIN_TOKEN })

    renderRoute('/aktivitet')

    expect(await screen.findByRole('button', { name: 'Skapa aktivitet' })).toBeInTheDocument()
    // Admin ser den hanterbara tabellen (ändra/ställ in/ta bort), inte bara läslistan (#408).
    expect(
      await screen.findByRole('button', { name: 'Ändra Borta mot Torslanda' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ta bort Borta mot Torslanda' })).toBeInTheDocument()
  })

  it('en admin kan ställa in och ta bort en aktivitet (#408)', async () => {
    setAccessToken(ADMIN_TOKEN)
    const sent: { url: string; method: string }[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown, init?: RequestInit) => {
        const url = String(input)
        const method = init?.method ?? 'GET'
        sent.push({ url, method })
        if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
        if (url.includes('/auth/refresh'))
          return Promise.resolve(jsonResponse({ accessToken: ADMIN_TOKEN }))
        if (url.includes('/api/v1/trupper/mina')) {
          return Promise.resolve(
            jsonResponse([{ id: TRUPP, clubName: 'Kärra', name: 'P2016', season: '2026' }]),
          )
        }
        if (url.includes(`/api/v1/trupper/${TRUPP}/events`)) {
          return Promise.resolve(
            jsonResponse([
              {
                event: {
                  id: 'm1',
                  type: 'Match',
                  kickoffUtc: FUTURE,
                  title: null,
                  opponent: 'Torslanda',
                  isHome: false,
                  status: 'Scheduled',
                  address: 'Klarebergsvallen',
                  venue: {
                    name: 'Klarebergsvallen',
                    address: 'Klarebergsvallen',
                    latitude: 57.8,
                    longitude: 12,
                  },
                },
                team: { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' },
              },
            ]),
          )
        }
        if (url.includes('/api/v1/hem')) {
          return Promise.resolve(
            jsonResponse({ nextEvent: null, pendingKallelser: [], latestChat: null }),
          )
        }
        // Admin-muteringarna svarar ok.
        return Promise.resolve(jsonResponse({ id: 'm1', status: 'Cancelled' }))
      }),
    )
    const user = userEvent.setup()

    const { queryClient } = renderRoute('/aktivitet')
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    // Ställ in (ConfirmButton: klick + bekräfta) → POST .../events/m1/cancel.
    await user.click(await screen.findByRole('button', { name: 'Ställ in Borta mot Torslanda' }))
    await user.click(screen.getByRole('button', { name: 'Ställ in' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.method === 'POST' &&
            r.url.includes(`/api/v1/admin/trupper/${TRUPP}/events/m1/cancel`),
        ),
      ).toBe(true)
    })

    // En lag-riktad match som ställs in här måste också uppdatera lagets schema och
    // trupp-cuplistan, annars står den kvar inaktuell där (#537).
    await waitFor(() => {
      expect(invalidate).toHaveBeenCalledWith({ queryKey: ['team-events'] })
      expect(invalidate).toHaveBeenCalledWith({ queryKey: ['cup', 'trupp', TRUPP] })
    })

    // Ett role=status-kvitto annonserar att det gick (#544).
    expect(await screen.findByText('Aktiviteten ställdes in.')).toBeInTheDocument()

    // Ta bort → bekräftelsepanel → DELETE .../events/m1.
    await user.click(screen.getByRole('button', { name: 'Ta bort Borta mot Torslanda' }))
    await user.click(await screen.findByRole('button', { name: 'Ja, ta bort' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.method === 'DELETE' && r.url.includes(`/api/v1/admin/trupper/${TRUPP}/events/m1`),
        ),
      ).toBe(true)
    })

    // Kvitto efter borttagning, och fokus flyttas dit i stället för att falla till <body> när
    // danger-zonen (med "Ja, ta bort") unmontas (#544, WCAG 2.4.3).
    const removed = await screen.findByText('Aktiviteten togs bort.')
    await waitFor(() => expect(removed).toHaveFocus())
  })

  it('visar ett tomläge när truppen saknar aktiviteter', async () => {
    setAccessToken(PARENT_TOKEN)
    stub({ token: PARENT_TOKEN, activities: [] })

    renderRoute('/aktivitet')

    expect(await screen.findByText('Inga aktiviteter är inlagda än.')).toBeInTheDocument()
  })
})
