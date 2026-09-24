import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  createClub,
  createSport,
  createTrupp,
  getAdmins,
  getClubs,
  getSports,
  getTrupper,
  grantAdmin,
  revokeAdmin,
  updateClub,
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
    mutationFn: (input: { clubId: string; sportId: string; name: string }) =>
      createTrupp(input.clubId, input.sportId, input.name),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.trupper }),
  })
}

export function useUpdateTrupp() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { id: string; sportId: string; name: string }) =>
      updateTrupp(input.id, input.sportId, input.name),
    onSuccess: () => client.invalidateQueries({ queryKey: superadminKeys.trupper }),
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
