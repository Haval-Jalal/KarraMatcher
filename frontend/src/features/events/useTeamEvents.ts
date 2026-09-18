import { useQuery } from '@tanstack/react-query'

import { getJson } from '@/lib/api'

import type { TeamEventSchedule } from './types'

export const teamEventsQueryKey = (slug: string) => ['team-events', slug] as const

/**
 * Lagets hela schema av händelser.
 *
 * Stängd i v2 (§KM.3): kräver inloggning och medlemskap i laget.
 */
export function useTeamEvents(slug: string) {
  return useQuery({
    queryKey: teamEventsQueryKey(slug),
    queryFn: ({ signal }) => getJson<TeamEventSchedule>(`/api/v1/teams/${slug}/events`, signal),
  })
}
