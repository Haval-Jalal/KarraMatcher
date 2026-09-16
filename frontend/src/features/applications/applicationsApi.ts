import { getJson, postJson } from '@/lib/api'

/**
 * Ansökningarnas API (§KM.3, `#194`).
 *
 * Trupp-infon är anonym (trupp-id i handen räcker), ansökan kräver inloggning, kön och
 * besluten kräver admin för truppen server-side. Klienten göms för andra, men det är servern
 * som nekar.
 */

export interface ApplyInfo {
  truppName: string
}

export interface Application {
  id: string
  applicantName: string | null
  applicantEmail: string
  status: string
  createdUtc: string
}

// ---- Förälderns sida ----------------------------------------------------------------
export const getApplyInfo = (truppId: string): Promise<ApplyInfo> =>
  getJson<ApplyInfo>(`/api/v1/trupper/${truppId}/apply-info`)

export const apply = (truppId: string): Promise<void> =>
  postJson<void>(`/api/v1/trupper/${truppId}/applications`)

// ---- Admin -------------------------------------------------------------------------
export const getApplications = (truppId: string): Promise<Application[]> =>
  getJson<Application[]>(`/api/v1/admin/trupper/${truppId}/applications`)

export const approveApplication = (truppId: string, id: string): Promise<void> =>
  postJson<void>(`/api/v1/admin/trupper/${truppId}/applications/${id}/approve`)

export const denyApplication = (truppId: string, id: string): Promise<void> =>
  postJson<void>(`/api/v1/admin/trupper/${truppId}/applications/${id}/deny`)
