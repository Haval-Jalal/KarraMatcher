import { useQuery } from '@tanstack/react-query'

import { getJson } from '@/lib/api'

import type { MatchDetail } from './types'

export const matchQueryKey = (id: string) => ['match', id] as const

/**
 * En enskild match med spelplats, koordinater och lag.
 *
 * Publik och cachad fem minuter på Vercels edge, så en förälder som öppnar en delad länk
 * eller en kalenderpost får oftast svar utan att Render väcks (§KM.11).
 */
export function useMatch(id: string) {
  return useQuery({
    queryKey: matchQueryKey(id),
    queryFn: ({ signal }) => getJson<MatchDetail>(`/api/v1/matches/${id}`, signal),
  })
}
