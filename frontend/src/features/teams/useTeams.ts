import { useQuery } from '@tanstack/react-query'

import { getJson } from '@/lib/api'

import type { Team } from './types'

export const teamsQueryKey = ['teams'] as const

/**
 * Lagen för lagväljaren.
 *
 * Endpointen är publik och cachas på Vercels edge i en timme, så det här är ett billigt
 * anrop även när Render sover. Klienten håller den längre än standard eftersom lagen i
 * praktiken aldrig ändras under en säsong.
 */
export function useTeams() {
  return useQuery({
    queryKey: teamsQueryKey,
    queryFn: ({ signal }) => getJson<Team[]>('/api/v1/teams', signal),
    staleTime: 60 * 60 * 1000,
  })
}
