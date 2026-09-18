import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  createChild,
  deleteChild,
  getRoster,
  linkGuardian,
  unlinkGuardian,
  updateChild,
  type ChildInput,
} from './childrenApi'

/** Data och mutationer för barnhantering (§KM.1, `#196`). */

export const childrenKeys = {
  roster: (truppId: string) => ['admin', 'roster', truppId] as const,
}

export function useRoster(truppId: string | null) {
  return useQuery({
    queryKey: childrenKeys.roster(truppId ?? ''),
    queryFn: () => getRoster(truppId as string),
    enabled: truppId !== null,
  })
}

export function useCreateChild(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: ChildInput) => createChild(truppId, input),
    onSuccess: () => client.invalidateQueries({ queryKey: childrenKeys.roster(truppId) }),
  })
}

export function useUpdateChild(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { id: string; data: ChildInput }) =>
      updateChild(truppId, input.id, input.data),
    onSuccess: () => client.invalidateQueries({ queryKey: childrenKeys.roster(truppId) }),
  })
}

export function useDeleteChild(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteChild(truppId, id),
    onSuccess: () => client.invalidateQueries({ queryKey: childrenKeys.roster(truppId) }),
  })
}

export function useLinkGuardian(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { childId: string; email: string }) =>
      linkGuardian(truppId, input.childId, input.email),
    onSuccess: () => client.invalidateQueries({ queryKey: childrenKeys.roster(truppId) }),
  })
}

export function useUnlinkGuardian(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { childId: string; accountId: string }) =>
      unlinkGuardian(truppId, input.childId, input.accountId),
    onSuccess: () => client.invalidateQueries({ queryKey: childrenKeys.roster(truppId) }),
  })
}
