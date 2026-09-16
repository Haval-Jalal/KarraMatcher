import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  createClub,
  createLag,
  createSport,
  createTrupp,
  getAdmins,
  getClubs,
  getLag,
  getSports,
  getTrupper,
  grantAdmin,
  revokeAdmin,
  updateClub,
  updateLag,
  updateSport,
  updateTrupp,
} from './superadminApi'

/**
 * Data och mutationer för superadmin-konsolen (§KM.3, `#192`).
 *
 * En mutation invaliderar den lista den rör, så vyn speglar servern utan att sidan laddas
 * om. Ingen <c>staleTime</c>: strukturen ändras sällan men ska visas färsk när superadmin
 * själv nyss ändrat den.
 */

export const superadminKeys = {
  sports: ['admin', 'sports'] as const,
  clubs: ['admin', 'clubs'] as const,
  trupper: ['admin', 'trupper'] as const,
  lag: (truppId: string) => ['admin', 'lag', truppId] as const,
  admins: (truppId: string) => ['admin', 'admins', truppId] as const,
}

export function useSports() {
  return useQuery({ queryKey: superadminKeys.sports, queryFn: () => getSports() })
}

export function useCreateSport() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ name, slug }: { name: string; slug: string }) => createSport(name, slug),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.sports }),
  })
}

export function useUpdateSport() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => updateSport(id, name),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.sports }),
  })
}

export function useClubs() {
  return useQuery({ queryKey: superadminKeys.clubs, queryFn: () => getClubs() })
}

export function useCreateClub() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ name, slug }: { name: string; slug: string }) => createClub(name, slug),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.clubs }),
  })
}

export function useUpdateClub() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => updateClub(id, name),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.clubs }),
  })
}

export function useTrupper() {
  return useQuery({ queryKey: superadminKeys.trupper, queryFn: () => getTrupper() })
}

export function useCreateTrupp() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { clubId: string; sportId: string; name: string; season: string }) =>
      createTrupp(input.clubId, input.sportId, input.name, input.season),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.trupper }),
  })
}

export function useUpdateTrupp() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { id: string; sportId: string; name: string; season: string }) =>
      updateTrupp(input.id, input.sportId, input.name, input.season),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.trupper }),
  })
}

export function useLag(truppId: string | null) {
  return useQuery({
    queryKey: superadminKeys.lag(truppId ?? ''),
    queryFn: () => getLag(truppId as string),
    enabled: truppId !== null,
  })
}

export function useCreateLag(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { name: string; colorHex: string; slug: string }) =>
      createLag(truppId, input.name, input.colorHex, input.slug),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.lag(truppId) }),
  })
}

export function useUpdateLag(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { id: string; name: string; colorHex: string }) =>
      updateLag(input.id, input.name, input.colorHex),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.lag(truppId) }),
  })
}

export function useAdmins(truppId: string | null) {
  return useQuery({
    queryKey: superadminKeys.admins(truppId ?? ''),
    queryFn: () => getAdmins(truppId as string),
    enabled: truppId !== null,
  })
}

export function useGrantAdmin(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (email: string) => grantAdmin(truppId, email),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.admins(truppId) }),
  })
}

export function useRevokeAdmin(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (accountId: string) => revokeAdmin(truppId, accountId),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.admins(truppId) }),
  })
}
