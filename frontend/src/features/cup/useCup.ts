import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  assignCupChild,
  createCupTeam,
  deleteCupTeam,
  renameCupTeam,
  getCupSummary,
  getTruppCups,
  openCup,
  signUpChild,
  unassignCupChild,
  withdrawChild,
} from './cupApi'

/** Data och mutationer för cupens öppna anmälan (`#295`/`#296`/`#304`). */

export const cupKeys = {
  summary: (eventId: string) => ['cup', 'summary', eventId] as const,
  truppCups: (truppId: string) => ['cup', 'trupp', truppId] as const,
}

export function useCupSummary(eventId: string) {
  return useQuery({
    queryKey: cupKeys.summary(eventId),
    queryFn: ({ signal }) => getCupSummary(eventId, signal),
    // Först-till-kvarn med hårt platstak, och lagplaceringen läses härifrån: en plats som redan är
    // tagen (eller ett nytt lagval) ska inte se inaktuell ut för en samtidig användare (#588). Samma
    // färskhet som samåkningen och kallelse-summeringarna, inte den globala 60s-cachen.
    staleTime: 0,
  })
}

export function useTruppCups(truppId: string | null) {
  return useQuery({
    queryKey: cupKeys.truppCups(truppId ?? ''),
    queryFn: ({ signal }) => getTruppCups(truppId as string, signal),
    enabled: truppId !== null,
    // Listan visar "platser kvar"/"Fullt" per cup — håll den live av samma skäl (#588).
    staleTime: 0,
  })
}

/**
 * Både cupens egen sammanställning och truppens cup-lista påverkas: listan visar öppet-läge och
 * platser-kvar, som ändras när en cup öppnas eller ett barn anmäls/dras tillbaka (#399).
 */
function invalidateCup(
  client: ReturnType<typeof useQueryClient>,
  truppId: string,
  eventId: string,
): Promise<void> {
  return Promise.all([
    client.invalidateQueries({ queryKey: cupKeys.summary(eventId) }),
    client.invalidateQueries({ queryKey: cupKeys.truppCups(truppId) }),
  ]).then(() => undefined)
}

export function useOpenCup(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (capacity: number) => openCup(truppId, eventId, capacity),
    onSuccess: () => invalidateCup(client, truppId, eventId),
  })
}

export function useSignUpChild(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (childId: string) => signUpChild(eventId, childId),
    onSuccess: () => invalidateCup(client, truppId, eventId),
  })
}

export function useWithdrawChild(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (childId: string) => withdrawChild(eventId, childId),
    onSuccess: () => invalidateCup(client, truppId, eventId),
  })
}

/** Cup-lags-mutationer (`#335`) — alla speglas tillbaka i cupens sammanställning. */
export function useCreateCupTeam(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (name: string) => createCupTeam(truppId, eventId, name),
    onSuccess: () => client.invalidateQueries({ queryKey: cupKeys.summary(eventId) }),
  })
}

export function useRenameCupTeam(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ cupTeamId, name }: { cupTeamId: string; name: string }) =>
      renameCupTeam(truppId, eventId, cupTeamId, name),
    onSuccess: () => client.invalidateQueries({ queryKey: cupKeys.summary(eventId) }),
  })
}

export function useDeleteCupTeam(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (cupTeamId: string) => deleteCupTeam(truppId, eventId, cupTeamId),
    onSuccess: () => client.invalidateQueries({ queryKey: cupKeys.summary(eventId) }),
  })
}

export function useAssignCupChild(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ cupTeamId, childId }: { cupTeamId: string; childId: string }) =>
      assignCupChild(truppId, eventId, cupTeamId, childId),
    onSuccess: () => client.invalidateQueries({ queryKey: cupKeys.summary(eventId) }),
  })
}

export function useUnassignCupChild(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ cupTeamId, childId }: { cupTeamId: string; childId: string }) =>
      unassignCupChild(truppId, eventId, cupTeamId, childId),
    onSuccess: () => client.invalidateQueries({ queryKey: cupKeys.summary(eventId) }),
  })
}
