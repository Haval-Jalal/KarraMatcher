import { useQuery } from '@tanstack/react-query'

import { getJson } from '@/lib/api'

import type { TeamMatches } from './types'

export const teamMatchesQueryKey = (slug: string) => ['team-matches', slug] as const

/**
 * Lagets hela schema.
 *
 * Endpointen är publik och cachas fem minuter på Vercels edge, så en förälder som öppnar
 * appen lördag morgon får oftast svar utan att Render behöver väckas (§KM.11).
 */
export function useTeamMatches(slug: string) {
  return useQuery({
    queryKey: teamMatchesQueryKey(slug),
    queryFn: ({ signal }) => getJson<TeamMatches>(`/api/v1/teams/${slug}/matches`, signal),
  })
}
