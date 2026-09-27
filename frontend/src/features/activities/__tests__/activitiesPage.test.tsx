import { screen } from '@testing-library/react'
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
    // Läslistan finns ändå.
    expect(await screen.findByText(/Torslanda/)).toBeInTheDocument()
  })

  it('visar ett tomläge när truppen saknar aktiviteter', async () => {
    setAccessToken(PARENT_TOKEN)
    stub({ token: PARENT_TOKEN, activities: [] })

    renderRoute('/aktivitet')

    expect(await screen.findByText('Inga aktiviteter är inlagda än.')).toBeInTheDocument()
  })
})
