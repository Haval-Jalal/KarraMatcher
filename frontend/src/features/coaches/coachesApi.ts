import { getJson, postJson } from '@/lib/api'

/** Tränartillsättning per lag (§KM.3, `#197`). Allt kräver admin för truppen server-side. */

export interface Coach {
  accountId: string
  displayName: string | null
  email: string
  grantedUtc: string
}

export interface CoachTeam {
  teamId: string
  teamName: string
  colorHex: string
  coaches: Coach[]
}

export interface TruppCoaches {
  teams: CoachTeam[]
}

const base = (truppId: string) => `/api/v1/admin/trupper/${truppId}`

export const getCoaches = (truppId: string): Promise<TruppCoaches> =>
  getJson<TruppCoaches>(`${base(truppId)}/coaches`)

export const grantCoach = (truppId: string, teamId: string, email: string): Promise<Coach> =>
  postJson<Coach>(`${base(truppId)}/teams/${teamId}/coaches`, { email })

export const revokeCoach = (truppId: string, teamId: string, accountId: string): Promise<void> =>
  postJson<void>(`${base(truppId)}/teams/${teamId}/coaches/${accountId}`, undefined, {
    method: 'DELETE',
  })
