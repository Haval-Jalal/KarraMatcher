import { getJson, postJson } from '@/lib/api'

/**
 * Superadmins plattforms-API (§KM.3, `#192`).
 *
 * Alla anrop kräver superadmin server-side (policyn <c>SuperAdmin</c>). Klienten göms för
 * andra, men det är servern som nekar — de här funktionerna svarar `403` för alla utom dig.
 */

export interface Sport {
  id: string
  name: string
  slug: string
}

export interface Club {
  id: string
  name: string
  slug: string
}

export interface Trupp {
  id: string
  clubId: string
  clubName: string
  sportId: string
  sportName: string
  name: string
}

export interface TruppAdmin {
  accountId: string
  displayName: string | null
  email: string
  grantedUtc: string
}

// ---- Sport -------------------------------------------------------------------------
export const getSports = (): Promise<Sport[]> => getJson<Sport[]>('/api/v1/admin/sports')

export const createSport = (name: string, slug: string): Promise<Sport> =>
  postJson<Sport>('/api/v1/admin/sports', { name, slug })

export const updateSport = (id: string, name: string): Promise<Sport> =>
  postJson<Sport>(`/api/v1/admin/sports/${id}`, { name }, { method: 'PUT' })

// ---- Klubb -------------------------------------------------------------------------
export const getClubs = (): Promise<Club[]> => getJson<Club[]>('/api/v1/admin/clubs')

export const createClub = (name: string, slug: string): Promise<Club> =>
  postJson<Club>('/api/v1/admin/clubs', { name, slug })

export const updateClub = (id: string, name: string): Promise<Club> =>
  postJson<Club>(`/api/v1/admin/clubs/${id}`, { name }, { method: 'PUT' })

// ---- Trupp -------------------------------------------------------------------------
export const getTrupper = (clubId?: string): Promise<Trupp[]> =>
  getJson<Trupp[]>(
    clubId === undefined ? '/api/v1/admin/trupper' : `/api/v1/admin/trupper?clubId=${clubId}`,
  )

export const createTrupp = (clubId: string, sportId: string, name: string): Promise<Trupp> =>
  postJson<Trupp>('/api/v1/admin/trupper', { clubId, sportId, name })

export const updateTrupp = (id: string, sportId: string, name: string): Promise<Trupp> =>
  postJson<Trupp>(`/api/v1/admin/trupper/${id}`, { sportId, name }, { method: 'PUT' })

// ---- Admins ------------------------------------------------------------------------
export const getAdmins = (truppId: string): Promise<TruppAdmin[]> =>
  getJson<TruppAdmin[]>(`/api/v1/admin/trupper/${truppId}/admins`)

export const grantAdmin = (truppId: string, email: string): Promise<TruppAdmin> =>
  postJson<TruppAdmin>(`/api/v1/admin/trupper/${truppId}/admins`, { email })

export const revokeAdmin = (truppId: string, accountId: string): Promise<void> =>
  postJson<void>(`/api/v1/admin/trupper/${truppId}/admins/${accountId}`, undefined, {
    method: 'DELETE',
  })
