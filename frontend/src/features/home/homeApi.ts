import { getAuthJson } from '@/lib/api'

/**
 * Hem-vyns sammanställning (`GET /api/v1/hem`). Speglar `HomeSummaryDto` i backend.
 *
 * Allt är sådant den inloggade ändå får se, sammanställt i ett anrop: nästa händelse i något av
 * hens lag, kallelser som väntar på svar för hens barn, och det senaste i en kanal hen når.
 * Tiderna är UTC och konverteras i `@/lib/time`, aldrig här (§KM.5).
 */
export interface HomeEvent {
  id: string
  type: 'Match' | 'Training' | 'Other' | 'Cup'
  kickoffUtc: string
  title: string | null
  opponent: string | null
  isHome: boolean | null
  teamSlug: string
  teamName: string
  place: string
}

export interface HomePendingKallelse {
  eventId: string
  type: 'Match' | 'Training' | 'Other' | 'Cup'
  kickoffUtc: string
  title: string | null
  opponent: string | null
  isHome: boolean | null
  teamName: string
  /** Antalet av dina barn utan svar — aldrig barnens namn. */
  unansweredCount: number
}

export interface HomeChat {
  truppId: string
  /** Lagets slug för en lag-kanal, annars null för truppens primärkanal. */
  teamSlug: string | null
  channelName: string
  authorName: string
  snippet: string
  sentAtUtc: string
}

export interface HomeSummary {
  nextEvent: HomeEvent | null
  pendingKallelser: HomePendingKallelse[]
  latestChat: HomeChat | null
}

export const getHomeSummary = (signal?: AbortSignal): Promise<HomeSummary> =>
  getAuthJson<HomeSummary>('/api/v1/hem', signal)
