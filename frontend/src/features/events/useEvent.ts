import { useQuery } from '@tanstack/react-query'

import { getJson } from '@/lib/api'

import type { EventDetail } from './types'

export const eventQueryKey = (id: string) => ['event', id] as const

/**
 * En enskild händelse med spelplats, koordinater och lag.
 *
 * Stängd i v2 (§KM.3): kräver inloggning och medlemskap i händelsens lag.
 */
export function useEvent(id: string) {
  return useQuery({
    queryKey: eventQueryKey(id),
    queryFn: ({ signal }) => getJson<EventDetail>(`/api/v1/events/${id}`, signal),
  })
}
