import { getJson, postJson } from '@/lib/api'

/**
 * Lag-hanteringen (kodnamn <c>Team</c>) i en trupp (`#192`, `#261`).
 *
 * Lagen är truppens admins ansvar, så endpointen ligger under truppen och gården är
 * <c>AdminOfTrupp</c> server-side (superadmin kortsluts). Panelen används i admin-vyn.
 */

export interface Lag {
  id: string
  truppId: string
  name: string
  colorHex: string
  slug: string
}

export const getLag = (truppId: string): Promise<Lag[]> =>
  getJson<Lag[]>(`/api/v1/admin/trupper/${truppId}/lag`)

export const createLag = (
  truppId: string,
  name: string,
  colorHex: string,
  slug: string,
): Promise<Lag> => postJson<Lag>(`/api/v1/admin/trupper/${truppId}/lag`, { name, colorHex, slug })

/**
 * Ändrar ett lags namn och färg. Slugen är stabil (lever i delade länkar) och byts inte här.
 * `truppId` ligger i adressen så servern kan verifiera att laget hör dit (IDOR-vakt, §KM.3).
 */
export const updateLag = (
  truppId: string,
  id: string,
  name: string,
  colorHex: string,
): Promise<Lag> =>
  postJson<Lag>(`/api/v1/admin/trupper/${truppId}/lag/${id}`, { name, colorHex }, { method: 'PUT' })

/**
 * Slår på eller av kallelsen för ett lag (§KM.7). Trupp-adminens beslut, inte tränarens; servern
 * verifierar att laget hör till truppen (IDOR-skydd).
 */
export const setLagAttendance = (truppId: string, id: string, enabled: boolean): Promise<void> =>
  postJson<void>(
    `/api/v1/admin/trupper/${truppId}/lag/${id}/attendance`,
    { enabled },
    { method: 'PUT' },
  )
