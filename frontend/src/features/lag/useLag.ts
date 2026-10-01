import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { childrenKeys } from '@/features/children/useChildren'
import { teamsQueryKey } from '@/features/teams/useTeams'

import { createLag, getLag, updateLag } from './lagApi'

const lagKey = (truppId: string) => ['admin', 'lag', truppId] as const

/**
 * Ett nytt lag eller ett namn-/färgbyte syns i långt fler vyer än admin-lag-listan, och flera av
 * dem är långlivade i cachen: lagväljaren + temafärgen (`['teams']`, staleTime 1h), adminens
 * trupp-roster och tränarens lag-roster (som bär lagets namn/färg), och lag-schemats rubrik
 * (`['team-events']`). Utan en bred invalidering står de kvar inaktuella i upp till en timme (#536).
 * `useAcceptInvitation` invaliderar `teamsQueryKey` av samma skäl.
 */
function invalidateLagViews(
  client: ReturnType<typeof useQueryClient>,
  truppId: string,
): Promise<unknown> {
  return Promise.all([
    client.invalidateQueries({ queryKey: lagKey(truppId) }),
    client.invalidateQueries({ queryKey: teamsQueryKey }),
    client.invalidateQueries({ queryKey: childrenKeys.roster(truppId) }),
    // Tränarens lag-roster är nycklad på lagets slug, som mutationen inte känner till — invalidera
    // via prefixet (som barn-ändringarna i useChildren).
    client.invalidateQueries({ queryKey: childrenKeys.teamRosters() }),
    // Lag-schemats rubrik bär lagets namn/färg; nyckeln är per slug, så invalidera prefixet.
    client.invalidateQueries({ queryKey: ['team-events'] }),
  ])
}

export function useLag(truppId: string) {
  return useQuery({ queryKey: lagKey(truppId), queryFn: () => getLag(truppId) })
}

export function useCreateLag(truppId: string) {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (input: { name: string; colorHex: string; slug: string }) =>
      createLag(truppId, input.name, input.colorHex, input.slug),
    onSuccess: () => invalidateLagViews(client, truppId),
  })
}

export function useUpdateLag(truppId: string) {
  const client = useQueryClient()

  return useMutation({
    mutationFn: (input: { id: string; name: string; colorHex: string }) =>
      updateLag(truppId, input.id, input.name, input.colorHex),
    onSuccess: () => invalidateLagViews(client, truppId),
  })
}
