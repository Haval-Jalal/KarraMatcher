import { getJson, postJson } from '@/lib/api'
import type { Match } from '@/features/matches'

/** En spelplats i registret. */
export interface Venue {
  id: string
  name: string
  address: string
  isHome: boolean
}

/** Det tränaren fyller i om en match. Tiden är redan omräknad till UTC. */
export interface MatchInput {
  kickoffUtc: string
  opponent: string
  venueId: string
  isHome: boolean
  note: string | null
}

export function searchVenues(term: string): Promise<Venue[]> {
  return getJson<Venue[]>(`/api/v1/venues?q=${encodeURIComponent(term)}`)
}

export function createMatch(slug: string, input: MatchInput): Promise<Match> {
  return postJson<Match>(`/api/v1/teams/${encodeURIComponent(slug)}/matches`, input)
}

export function updateMatch(slug: string, id: string, input: MatchInput): Promise<Match> {
  return postJson<Match>(`/api/v1/teams/${encodeURIComponent(slug)}/matches/${id}`, input, {
    method: 'PUT',
  })
}

/**
 * Ställer in en match.
 *
 * Inte samma sak som att ta bort den: kalenderposten ska bli kvar, markerad som inställd,
 * annars står matchen kvar i föräldrarnas kalendrar som om ingenting hänt (§KM.4).
 */
export function cancelMatch(slug: string, id: string): Promise<Match> {
  return postJson<Match>(`/api/v1/teams/${encodeURIComponent(slug)}/matches/${id}/cancel`)
}

/** Tar bort en match som aldrig skulle ha lagts in. */
export function deleteMatch(slug: string, id: string): Promise<void> {
  return postJson<void>(`/api/v1/teams/${encodeURIComponent(slug)}/matches/${id}`, undefined, {
    method: 'DELETE',
  })
}
