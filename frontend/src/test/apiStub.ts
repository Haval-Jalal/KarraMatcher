import { vi } from 'vitest'

import type { TeamEvent } from '@/features/events'
import type { Team } from '@/features/teams'

export const testTeams: Team[] = [
  { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' },
  { slug: 'bla', name: 'Blå', ageGroup: 'P2016', colorHex: '#1E3F8A' },
]

export function testEvent(
  id: string,
  kickoffUtc: string,
  overrides: Partial<TeamEvent> = {},
): TeamEvent {
  return {
    id,
    type: 'Match',
    kickoffUtc,
    title: null,
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

/**
 * Ett svar som beter sig som fetch gör — inklusive `text()`.
 *
 * API-klienten läser kroppen som text för att tomma svar ska gå att skilja från trasiga
 * (se `parseBody` i `lib/api.ts`).
 */
export function jsonResponse(body: unknown, status = 200): Response {
  const text = JSON.stringify(body)

  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(text),
  } as unknown as Response
}

/** Ett svar utan kropp, som 202 från `request-code` och 204 från `logout`. */
export function emptyResponse(status: number): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.reject(new SyntaxError('Unexpected end of JSON input')),
    text: () => Promise.resolve(''),
  } as unknown as Response
}

/**
 * Svarar som API:t gör, per adress (`#198`).
 *
 * Fixturnycklarna heter fortfarande `matches`/`match` (och bär den inre nyckeln likaså) —
 * det är bara testhjälparens egna namn. Ut serialiseras svaret på den riktiga formen:
 * `{ team, events }` respektive `{ team, event }`, mot `/api/v1/events`.
 */
export function stubApi(options: {
  teams?: Team[] | 'error'
  matches?: { team: Team; matches: TeamEvent[] } | 'error' | 'notFound'
  match?: { team: Team; match: TeamEvent } | 'error' | 'notFound'
}) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      // Sessionen: appen försöker alltid förnya mot cookien vid kallstart (`#255`). En gäst
      // (inget konto satt i testet) får 401 och skickas till inloggningen. Ett inloggat test
      // sätter access-token direkt och når aldrig hit.
      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ title: 'Ingen session' }, 401))
      }

      // Hem-sammanställningen: tom som standard, så en vy som renderas i ett test som inte bryr
      // sig om den visar sina tomlägen i stället för att krascha på fel form.
      if (url.includes('/api/v1/hem')) {
        return Promise.resolve(
          jsonResponse({ nextEvent: null, pendingKallelser: [], latestChat: null }),
        )
      }

      // Kallelsen ligger under /api/v1/events/{id}/kallelse — fångas före händelse-detaljen.
      // Default: 404 (kallelsen inte påslagen), så en detaljsida som inte bryr sig om den
      // renderar ingen kallelse-sektion.
      if (url.includes('/kallelse')) {
        return Promise.resolve(jsonResponse({ title: 'Kallelsen är inte påslagen' }, 404))
      }

      // Enskild händelse: /api/v1/events/{id} innehåller också "/events".
      if (url.includes('/api/v1/events/')) {
        if (options.match === 'error') return Promise.reject(new TypeError('Failed to fetch'))
        if (options.match === 'notFound') {
          return Promise.resolve(jsonResponse({ title: 'Händelsen finns inte' }, 404))
        }
        const detail = options.match ?? {
          team: testTeams[0],
          match: testEvent('a', '2026-09-20T12:00:00Z'),
        }
        return Promise.resolve(
          jsonResponse({ team: detail.team, event: detail.match, truppId: 'trupp-stub' }),
        )
      }

      if (url.includes('/events')) {
        if (options.matches === 'error') return Promise.reject(new TypeError('Failed to fetch'))
        if (options.matches === 'notFound') {
          return Promise.resolve(jsonResponse({ title: 'Laget finns inte' }, 404))
        }
        const schedule = options.matches ?? { team: testTeams[0], matches: [] }
        return Promise.resolve(
          jsonResponse({ team: schedule.team, events: schedule.matches, truppId: 'trupp-stub' }),
        )
      }

      // Samåkning ligger kvar under /api/v1/matches/{id}/carpool (kvarhållet MatchId,
      // §KM.12). En detaljsida som renderas i ett test som inte bryr sig om den ska inte
      // krascha: tom lista.
      if (url.includes('/carpool')) return Promise.resolve(jsonResponse([]))

      if (options.teams === 'error') return Promise.reject(new TypeError('Failed to fetch'))
      return Promise.resolve(jsonResponse(options.teams ?? testTeams))
    }),
  )
}
