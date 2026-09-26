import { getAuthJson, postJson } from '@/lib/api'

/** Klubbens hemmaplan (`#307`). `configured` = en plan är satt (koordinater finns). */
export interface ClubVenue {
  name: string | null
  address: string | null
  latitude: number | null
  longitude: number | null
  configured: boolean
}

export const getClubVenue = (truppId: string, signal?: AbortSignal): Promise<ClubVenue> =>
  getAuthJson<ClubVenue>(`/api/v1/admin/trupper/${encodeURIComponent(truppId)}/club-venue`, signal)

/** Sätter (eller ändrar) klubbens hemmaplan. Servern geokodar adressen. */
export const setClubVenue = (truppId: string, name: string, address: string): Promise<void> =>
  postJson<void>(
    `/api/v1/admin/trupper/${encodeURIComponent(truppId)}/club-venue`,
    { name, address },
    { method: 'PUT' },
  )
