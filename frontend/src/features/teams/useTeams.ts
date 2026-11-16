import { useQuery } from '@tanstack/react-query'

import { getJson } from '@/lib/api'

import type { Team } from './types'

export const teamsQueryKey = ['teams'] as const

/**
 * Lagen för lagväljaren.
 *
 * Lagen ändras i praktiken aldrig under en säsong, så klienten håller dem länge (`staleTime` en
 * timme) och hämtar inte om dem i onödan. Endpointen kräver medlemskap och svaras `private,
 * no-store` som allt annat i v2 — ingen edge-cache (§KM.3/§KM.11); den långa färskheten ligger
 * alltså helt i klienten.
 */
export function useTeams() {
  return useQuery({
    queryKey: teamsQueryKey,
    queryFn: ({ signal }) => getJson<Team[]>('/api/v1/teams', signal),
    staleTime: 60 * 60 * 1000,
  })
}
