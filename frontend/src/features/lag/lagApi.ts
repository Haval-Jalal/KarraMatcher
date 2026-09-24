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
