/** En match så som API:t levererar den. Speglar `MatchDto` i backend. */
export interface Match {
  id: string
  /** Avspark i UTC. Konverteras till svensk tid i `@/lib/time`, aldrig här. */
  kickoffUtc: string
  opponent: string
  isHome: boolean
  status: 'Scheduled' | 'Cancelled' | 'Postponed'
  /** Matchens adress — spelplatsens, om matchen inte har en avvikande. */
  address: string
  venue: {
    name: string
    address: string
    latitude: number
    longitude: number
  }
}

export interface TeamMatches {
  team: {
    slug: string
    name: string
    ageGroup: string
    colorHex: string
  }
  matches: Match[]
}
