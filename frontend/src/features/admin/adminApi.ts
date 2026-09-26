import { getJson, postJson } from '@/lib/api'
import type { TeamEvent } from '@/features/events'

/** En spelplats i registret. */
export interface Venue {
  id: string
  name: string
  address: string
  isHome: boolean
}

/**
 * Det tränaren fyller i om en händelse (`#198`). Tiden är redan omräknad till UTC.
 *
 * `opponent` och `isHome` gäller en match; `title` en träning eller övrig händelse.
 */
export interface EventInput {
  type: 'Match' | 'Training' | 'Other' | 'Cup'
  kickoffUtc: string
  title: string | null
  opponent: string | null
  venueId: string
  isHome: boolean | null
  note: string | null
}

export function searchVenues(term: string): Promise<Venue[]> {
  return getJson<Venue[]>(`/api/v1/venues?q=${encodeURIComponent(term)}`)
}

export function createEvent(slug: string, input: EventInput): Promise<TeamEvent> {
  return postJson<TeamEvent>(`/api/v1/teams/${encodeURIComponent(slug)}/events`, input)
}

export function updateEvent(slug: string, id: string, input: EventInput): Promise<TeamEvent> {
  return postJson<TeamEvent>(`/api/v1/teams/${encodeURIComponent(slug)}/events/${id}`, input, {
    method: 'PUT',
  })
}

/**
 * Ställer in en händelse.
 *
 * Inte samma sak som att ta bort den: kalenderposten ska bli kvar, markerad som inställd,
 * annars står den kvar i föräldrarnas kalendrar som om ingenting hänt (§KM.4).
 */
export function cancelEvent(slug: string, id: string): Promise<TeamEvent> {
  return postJson<TeamEvent>(`/api/v1/teams/${encodeURIComponent(slug)}/events/${id}/cancel`)
}

/** Tar bort en händelse som aldrig skulle ha lagts in. */
export function deleteEvent(slug: string, id: string): Promise<void> {
  return postJson<void>(`/api/v1/teams/${encodeURIComponent(slug)}/events/${id}`, undefined, {
    method: 'DELETE',
  })
}
