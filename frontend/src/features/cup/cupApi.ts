import { getAuthJson, postJson } from '@/lib/api'

/** Cupens öppna anmälan (§KM.12, `#295`/`#296`). Allt kräver inloggning; åtkomst prövas server-side. */

/** Ett anmält barn i sammanställningen. Visas som "Liam J" (§KM.1). */
export interface CupSignupChild {
  childId: string
  displayName: string
  teamName: string | null
  colorHex: string | null
}

/** Den inloggades eget barn i cupens trupp, och om det är anmält. */
export interface MyCupChild {
  childId: string
  displayName: string
  signedUp: boolean
}

/** Ett barn placerat i ett cup-lag (`#335`). Visas som "Liam J" (§KM.1). */
export interface CupTeamMember {
  childId: string
  displayName: string
}

/** Ett cup-lag som admin byggt av de anmälda barnen (`#335`). */
export interface CupTeam {
  id: string
  name: string
  members: CupTeamMember[]
}

/** Cupens anmälningsläge. */
export interface CupSummary {
  /** Sant när tränaren öppnat anmälan (ett platstak är satt). */
  open: boolean
  capacity: number | null
  spotsTaken: number
  spotsLeft: number
  isFull: boolean
  /** Alla anmälda barn — visas för truppens medlemmar. */
  signedUp: CupSignupChild[]
  /** Den inloggades egna barn att anmäla/avanmäla. */
  mine: MyCupChild[]
  /** Cup-lagen admin byggt (`#335`) — tomt tills admin skapat något. */
  teams: CupTeam[]
}

/** En cup i trupp-listan med sitt anmälningsläge (`#304`). */
export interface CupListItem {
  eventId: string
  title: string
  kickoffUtc: string
  teamName: string
  colorHex: string
  open: boolean
  capacity: number | null
  spotsLeft: number
  isFull: boolean
}

export const getCupSummary = (eventId: string, signal?: AbortSignal): Promise<CupSummary> =>
  getAuthJson<CupSummary>(`/api/v1/events/${encodeURIComponent(eventId)}/cup`, signal)

/** Truppens cuper med anmälningsläge — en trupp-vid lista (`#304`). */
export const getTruppCups = (truppId: string, signal?: AbortSignal): Promise<CupListItem[]> =>
  getAuthJson<CupListItem[]>(`/api/v1/trupper/${encodeURIComponent(truppId)}/cups`, signal)

/** Tränaren öppnar (eller ändrar) platstaket. Kräver AdminOfTrupp server-side. */
export const openCup = (truppId: string, eventId: string, capacity: number): Promise<void> =>
  postJson<void>(
    `/api/v1/admin/trupper/${encodeURIComponent(truppId)}/events/${encodeURIComponent(eventId)}/cup`,
    { capacity },
    { method: 'PUT' },
  )

/** Anmäler ett eget barn. Server avvisar en full cup (409). */
export const signUpChild = (eventId: string, childId: string): Promise<void> =>
  postJson<void>(
    `/api/v1/events/${encodeURIComponent(eventId)}/cup/children/${encodeURIComponent(childId)}`,
  )

/** Drar tillbaka en anmälan och frigör platsen. */
export const withdrawChild = (eventId: string, childId: string): Promise<void> =>
  postJson<void>(
    `/api/v1/events/${encodeURIComponent(eventId)}/cup/children/${encodeURIComponent(childId)}`,
    undefined,
    { method: 'DELETE' },
  )

/** Cup-lags-bygget (`#335`). Allt kräver AdminOfTrupp server-side. */
const cupTeamsBase = (truppId: string, eventId: string) =>
  `/api/v1/admin/trupper/${encodeURIComponent(truppId)}/events/${encodeURIComponent(eventId)}/cup/teams`

/** Skapar ett tomt cup-lag och ger tillbaka dess id. */
export const createCupTeam = (
  truppId: string,
  eventId: string,
  name: string,
): Promise<{ id: string }> => postJson<{ id: string }>(cupTeamsBase(truppId, eventId), { name })

/** Tar bort ett cup-lag (dess placeringar försvinner med det). */
export const deleteCupTeam = (truppId: string, eventId: string, cupTeamId: string): Promise<void> =>
  postJson<void>(`${cupTeamsBase(truppId, eventId)}/${encodeURIComponent(cupTeamId)}`, undefined, {
    method: 'DELETE',
  })

/** Placerar ett anmält barn i ett cup-lag (flyttar om det redan står i ett annat). */
export const assignCupChild = (
  truppId: string,
  eventId: string,
  cupTeamId: string,
  childId: string,
): Promise<void> =>
  postJson<void>(
    `${cupTeamsBase(truppId, eventId)}/${encodeURIComponent(cupTeamId)}/children/${encodeURIComponent(childId)}`,
    undefined,
    { method: 'PUT' },
  )

/** Tar bort ett barn ur ett cup-lag. */
export const unassignCupChild = (
  truppId: string,
  eventId: string,
  cupTeamId: string,
  childId: string,
): Promise<void> =>
  postJson<void>(
    `${cupTeamsBase(truppId, eventId)}/${encodeURIComponent(cupTeamId)}/children/${encodeURIComponent(childId)}`,
    undefined,
    { method: 'DELETE' },
  )
