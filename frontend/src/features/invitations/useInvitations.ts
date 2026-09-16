import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  acceptInvitation,
  createInvitation,
  getInvitations,
  getMyTrupper,
  previewInvitation,
  revokeInvitation,
} from './invitationsApi'

/** Data och mutationer för inbjudningar (§KM.3, `#193`). */

export const invitationKeys = {
  myTrupper: ['admin', 'my-trupper'] as const,
  list: (truppId: string) => ['admin', 'invitations', truppId] as const,
  preview: (token: string) => ['invitation-preview', token] as const,
}

export function useMyTrupper() {
  return useQuery({ queryKey: invitationKeys.myTrupper, queryFn: () => getMyTrupper() })
}

export function useInvitations(truppId: string | null) {
  return useQuery({
    queryKey: invitationKeys.list(truppId ?? ''),
    queryFn: () => getInvitations(truppId as string),
    enabled: truppId !== null,
  })
}

export function useCreateInvitation(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (email: string) => createInvitation(truppId, email),
    onSuccess: () => client.invalidateQueries({ queryKey: invitationKeys.list(truppId) }),
  })
}

export function useRevokeInvitation(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => revokeInvitation(truppId, id),
    onSuccess: () => client.invalidateQueries({ queryKey: invitationKeys.list(truppId) }),
  })
}

export function useInvitationPreview(token: string) {
  return useQuery({
    queryKey: invitationKeys.preview(token),
    queryFn: () => previewInvitation(token),
    retry: false,
  })
}

export function useAcceptInvitation(token: string) {
  return useMutation({ mutationFn: () => acceptInvitation(token) })
}
