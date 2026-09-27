import { useQuery } from '@tanstack/react-query'

import { getTruppActivities } from './activitiesApi'

/** Frågenyckeln för en trupps aktivitetslista — invalideras när en aktivitet skapas (`#334`). */
export const activitiesKeys = {
  list: (truppId: string) => ['activities', truppId] as const,
}

/** Truppens aktiviteter. Vilande tills en trupp är vald. */
export function useTruppActivities(truppId: string | null) {
  return useQuery({
    queryKey: activitiesKeys.list(truppId ?? ''),
    queryFn: () => getTruppActivities(truppId!),
    enabled: truppId !== null,
  })
}
