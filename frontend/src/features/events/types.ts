/** En händelse så som API:t levererar den. Speglar `EventDto` i backend (`#198`). */
export interface TeamEvent {
  id: string
  /** Match, träning eller övrigt. */
  type: 'Match' | 'Training' | 'Other'
  /** Starttid i UTC. Konverteras till svensk tid i `@/lib/time`, aldrig här. */
  kickoffUtc: string
  /** Rubrik för träning/övrigt. Null för en match (härleds ur motståndaren). */
  title: string | null
  /** Motståndare. Satt endast för en match. */
  opponent: string | null
  /** Hemma/borta. Satt endast för en match. */
  isHome: boolean | null
  status: 'Scheduled' | 'Cancelled' | 'Postponed'
  /** Händelsens adress — spelplatsens, om den inte har en avvikande. */
  address: string
  venue: {
    name: string
    address: string
    latitude: number
    longitude: number
  }
}

interface TeamSummary {
  slug: string
  name: string
  ageGroup: string
  colorHex: string
}

/** Svaret från `GET /api/v1/teams/{slug}/events`: laget, dess händelser och truppens id. */
export interface TeamEventSchedule {
  team: TeamSummary
  events: TeamEvent[]
  /** Åldersgruppens (truppens) id — låter en trupp-tränare känna igen sitt eget lag (§KM.7, `#287`). */
  truppId: string
}

/** Svaret från `GET /api/v1/events/{id}`: händelsen, dess lag och truppens id. */
export interface EventDetail {
  event: TeamEvent
  team: TeamSummary
  /** Åldersgruppens (truppens) id — som en admin behöver för kallelsen (§KM.7, `#199`). */
  truppId: string
}

/** Svensk etikett för händelsens typ. */
export function eventTypeLabel(type: TeamEvent['type']): string {
  switch (type) {
    case 'Match':
      return 'Match'
    case 'Training':
      return 'Träning'
    default:
      return 'Övrigt'
  }
}

/**
 * Kort etikett för en händelse: "Hemma mot X" för en match, annars rubriken.
 *
 * Ett ställe, så att kort, lista och detaljsida aldrig beskriver samma händelse olika.
 */
export function eventLabel(event: TeamEvent): string {
  if (event.type === 'Match') {
    return `${event.isHome ? 'Hemma' : 'Borta'} mot ${event.opponent ?? ''}`.trim()
  }

  return event.title ?? eventTypeLabel(event.type)
}
