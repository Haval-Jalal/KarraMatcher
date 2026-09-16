import { getJson, postJson } from '@/lib/api'

/**
 * Inbjudningarnas API (§KM.3, `#193`).
 *
 * Admin-anropen kräver admin för truppen server-side; förhandsvisningen är anonym (token i
 * handen räcker), accepten kräver inloggning som rätt adress. Klienten göms för andra, men
 * det är servern som nekar.
 */

/** En trupp den inloggade är admin för. */
export interface AdminTrupp {
  id: string
  clubName: string
  sportName: string
  name: string
  season: string
}

export interface Invitation {
  id: string
  email: string
  status: string
  teamId: string | null
  teamName: string | null
  createdUtc: string
  expiresUtc: string
}

export interface InvitationCreated {
  invitation: Invitation
  acceptUrl: string
}

export interface InvitationPreview {
  valid: boolean
  truppName: string
  lagName: string | null
  email: string
}

// ---- Admin -------------------------------------------------------------------------
export const getMyTrupper = (): Promise<AdminTrupp[]> =>
  getJson<AdminTrupp[]>('/api/v1/admin/my-trupper')

export const getInvitations = (truppId: string): Promise<Invitation[]> =>
  getJson<Invitation[]>(`/api/v1/admin/trupper/${truppId}/invitations`)

export const createInvitation = (truppId: string, email: string): Promise<InvitationCreated> =>
  postJson<InvitationCreated>(`/api/v1/admin/trupper/${truppId}/invitations`, { email })

export const revokeInvitation = (truppId: string, id: string): Promise<void> =>
  postJson<void>(`/api/v1/admin/trupper/${truppId}/invitations/${id}`, undefined, {
    method: 'DELETE',
  })

// ---- Förälderns sida ----------------------------------------------------------------
export const previewInvitation = (token: string): Promise<InvitationPreview> =>
  getJson<InvitationPreview>(`/api/v1/invitations/${token}`)

export const acceptInvitation = (token: string): Promise<{ truppName: string }> =>
  postJson<{ truppName: string }>(`/api/v1/invitations/${token}/accept`)
