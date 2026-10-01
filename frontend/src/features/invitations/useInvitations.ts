import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { chatKeys } from '@/features/chat/useChat'
import { homeSummaryQueryKey } from '@/features/home/useHomeSummary'
import { teamsQueryKey } from '@/features/teams/useTeams'

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
    mutationFn: ({ email, teamId }: { email: string; teamId: string | null }) =>
      createInvitation(truppId, email, teamId),
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
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => acceptInvitation(token),
    // Att gå med i en trupp ändrar medlemskaps-härledda vyer — lagväljaren, Hem-sammanställningen
    // och chattens trupplista. Utan detta visar Hem gammal data i upp till 60 s (`#486`).
    onSuccess: () =>
      Promise.all([
        client.invalidateQueries({ queryKey: teamsQueryKey }),
        client.invalidateQueries({ queryKey: homeSummaryQueryKey }),
        client.invalidateQueries({ queryKey: chatKeys.myTrupper }),
        client.invalidateQueries({ queryKey: invitationKeys.myTrupper }),
      ]),
  })
}
