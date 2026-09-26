import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { getCupSummary, getTruppCups, openCup, signUpChild, withdrawChild } from './cupApi'

/** Data och mutationer för cupens öppna anmälan (`#295`/`#296`/`#304`). */

export const cupKeys = {
  summary: (eventId: string) => ['cup', 'summary', eventId] as const,
  truppCups: (truppId: string) => ['cup', 'trupp', truppId] as const,
}

export function useCupSummary(eventId: string) {
  return useQuery({
    queryKey: cupKeys.summary(eventId),
    queryFn: ({ signal }) => getCupSummary(eventId, signal),
  })
}

export function useTruppCups(truppId: string | null) {
  return useQuery({
    queryKey: cupKeys.truppCups(truppId ?? ''),
    queryFn: ({ signal }) => getTruppCups(truppId as string, signal),
    enabled: truppId !== null,
  })
}

export function useOpenCup(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (capacity: number) => openCup(truppId, eventId, capacity),
    onSuccess: () => client.invalidateQueries({ queryKey: cupKeys.summary(eventId) }),
  })
}

export function useSignUpChild(eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (childId: string) => signUpChild(eventId, childId),
    onSuccess: () => client.invalidateQueries({ queryKey: cupKeys.summary(eventId) }),
  })
}

export function useWithdrawChild(eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (childId: string) => withdrawChild(eventId, childId),
    onSuccess: () => client.invalidateQueries({ queryKey: cupKeys.summary(eventId) }),
  })
}
