import { getJson, postJson } from '@/lib/api'

/** Barnhantering (§KM.1, `#196`). Allt kräver admin för truppen server-side. */

export interface Guardian {
  accountId: string
  displayName: string | null
  email: string
}

export interface Child {
  id: string
  firstName: string
  lastInitial: string
  displayName: string
  teamId: string | null
  teamName: string | null
  guardians: Guardian[]
}

export interface RosterTeam {
  id: string
  name: string
  colorHex: string
}

export interface Roster {
  teams: RosterTeam[]
  children: Child[]
}

export interface ChildInput {
  firstName: string
  lastInitial: string
  teamId: string | null
}

const base = (truppId: string) => `/api/v1/admin/trupper/${truppId}/children`

export const getRoster = (truppId: string): Promise<Roster> => getJson<Roster>(base(truppId))

export const createChild = (truppId: string, input: ChildInput): Promise<Child> =>
  postJson<Child>(base(truppId), input)

export const updateChild = (truppId: string, id: string, input: ChildInput): Promise<Child> =>
  postJson<Child>(`${base(truppId)}/${id}`, input, { method: 'PUT' })

export const deleteChild = (truppId: string, id: string): Promise<void> =>
  postJson<void>(`${base(truppId)}/${id}`, undefined, { method: 'DELETE' })

export const linkGuardian = (truppId: string, childId: string, email: string): Promise<void> =>
  postJson<void>(`${base(truppId)}/${childId}/guardians`, { email })

export const unlinkGuardian = (
  truppId: string,
  childId: string,
  accountId: string,
): Promise<void> =>
  postJson<void>(`${base(truppId)}/${childId}/guardians/${accountId}`, undefined, {
    method: 'DELETE',
  })
