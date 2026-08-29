import { vi } from 'vitest'

import type { Match, TeamMatches } from '@/features/matches'
import type { Team } from '@/features/teams'

export const testTeams: Team[] = [
  { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' },
  { slug: 'bla', name: 'Blå', ageGroup: 'P2016', colorHex: '#1E3F8A' },
]

export function testMatch(id: string, kickoffUtc: string, overrides: Partial<Match> = {}): Match {
  return {
    id,
    kickoffUtc,
    opponent: `Motstandare ${id}`,
    isHome: true,
    status: 'Scheduled',
    address: 'Klarebergsvallen, Karra',
    venue: {
      name: 'Klarebergsvallen',
      address: 'Klarebergsvallen, Karra',
      latitude: 57.8,
      longitude: 12,
    },
    ...overrides,
  }
}

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as unknown as Response
}

/**
 * Svarar som API:t gör, per adress. Utan detta skulle varje test behöva veta i vilken
 * ordning komponenterna råkar hämta — vilket är en ordning som inte bör spela roll.
 */
export function stubApi(options: {
  teams?: Team[] | 'error'
  matches?: TeamMatches | 'error' | 'notFound'
}) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/matches')) {
        if (options.matches === 'error') return Promise.reject(new TypeError('Failed to fetch'))
        if (options.matches === 'notFound') {
          return Promise.resolve(jsonResponse({ title: 'Laget finns inte' }, 404))
        }
        return Promise.resolve(jsonResponse(options.matches ?? { team: testTeams[0], matches: [] }))
      }

      if (options.teams === 'error') return Promise.reject(new TypeError('Failed to fetch'))
      return Promise.resolve(jsonResponse(options.teams ?? testTeams))
    }),
  )
}
