import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  createChild,
  deleteChild,
  getRoster,
  getTeamRoster,
  linkGuardian,
  unlinkGuardian,
  updateChild,
  type ChildInput,
} from './childrenApi'

/** Data och mutationer för barnhantering (§KM.1, `#196`). */

export const childrenKeys = {
  roster: (truppId: string) => ['admin', 'roster', truppId] as const,
  teamRoster: (slug: string) => ['team', 'roster', slug] as const,
  /** Prefix som matchar alla lag-rosters. En barn-ändring vet inte lagets slug, så tränarens
   *  "Laget"-lista invalideras brett (#399). */
  teamRosters: () => ['team', 'roster'] as const,
}

/**
 * En barn- eller vårdnadshavar-ändring syns i två vyer: adminens trupp-roster och tränarens
 * lag-roster. Den senare är nyckelad på lagets slug, som mutationen inte känner till, så den
 * invalideras via prefixet (#399).
 */
function invalidateRosters(
  client: ReturnType<typeof useQueryClient>,
  truppId: string,
): Promise<void> {
  return Promise.all([
    client.invalidateQueries({ queryKey: childrenKeys.roster(truppId) }),
    client.invalidateQueries({ queryKey: childrenKeys.teamRosters() }),
  ]).then(() => undefined)
}

export function useRoster(truppId: string | null) {
  return useQuery({
    queryKey: childrenKeys.roster(truppId ?? ''),
    queryFn: () => getRoster(truppId as string),
    enabled: truppId !== null,
  })
}

/** Lagtränarens läsvy över sitt lag (`#redesign`). */
export function useTeamRoster(slug: string) {
  return useQuery({
    queryKey: childrenKeys.teamRoster(slug),
    queryFn: () => getTeamRoster(slug),
  })
}

export function useCreateChild(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: ChildInput) => createChild(truppId, input),
    onSuccess: () => invalidateRosters(client, truppId),
  })
}

export function useUpdateChild(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { id: string; data: ChildInput }) =>
      updateChild(truppId, input.id, input.data),
    onSuccess: () => invalidateRosters(client, truppId),
  })
}

export function useDeleteChild(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteChild(truppId, id),
    onSuccess: () => invalidateRosters(client, truppId),
  })
}

export function useLinkGuardian(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { childId: string; email: string }) =>
      linkGuardian(truppId, input.childId, input.email),
    onSuccess: () => invalidateRosters(client, truppId),
  })
}

export function useUnlinkGuardian(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { childId: string; accountId: string }) =>
      unlinkGuardian(truppId, input.childId, input.accountId),
    onSuccess: () => invalidateRosters(client, truppId),
  })
}
