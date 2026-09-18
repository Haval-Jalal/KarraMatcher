import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { getCoaches, grantCoach, revokeCoach } from './coachesApi'

/** Data och mutationer för tränartillsättning (§KM.3, `#197`). */

export const coachesKeys = {
  trupp: (truppId: string) => ['admin', 'coaches', truppId] as const,
}

export function useCoaches(truppId: string | null) {
  return useQuery({
    queryKey: coachesKeys.trupp(truppId ?? ''),
    queryFn: () => getCoaches(truppId as string),
    enabled: truppId !== null,
  })
}

export function useGrantCoach(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { teamId: string; email: string }) =>
      grantCoach(truppId, input.teamId, input.email),
    onSuccess: () => client.invalidateQueries({ queryKey: coachesKeys.trupp(truppId) }),
  })
}

export function useRevokeCoach(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { teamId: string; accountId: string }) =>
      revokeCoach(truppId, input.teamId, input.accountId),
    onSuccess: () => client.invalidateQueries({ queryKey: coachesKeys.trupp(truppId) }),
  })
}
