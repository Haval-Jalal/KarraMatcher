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
