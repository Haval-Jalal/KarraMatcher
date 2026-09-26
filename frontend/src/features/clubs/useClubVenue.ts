import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { getClubVenue, setClubVenue } from './clubApi'

/** Data och mutation för klubbens hemmaplan (`#307`). */

export const clubVenueKey = (truppId: string) => ['club-venue', truppId] as const

export function useClubVenue(truppId: string) {
  return useQuery({
    queryKey: clubVenueKey(truppId),
    queryFn: ({ signal }) => getClubVenue(truppId, signal),
  })
}

export function useSetClubVenue(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { name: string; address: string }) =>
      setClubVenue(truppId, input.name, input.address),
    onSuccess: () => client.invalidateQueries({ queryKey: clubVenueKey(truppId) }),
  })
}
