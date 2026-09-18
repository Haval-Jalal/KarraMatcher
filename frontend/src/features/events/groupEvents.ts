import { formatMonthHeading, matchDayPosition, swedishDayKey } from '@/lib/time'

import type { TeamEvent } from './types'

export interface MonthGroup {
  /** Stabil nyckel för React, t.ex. `2026-10`. */
  key: string
  /** Rubriken som visas, t.ex. `Oktober 2026`. */
  heading: string
  events: TeamEvent[]
}

export interface GroupedEvents {
  today: TeamEvent[]
  upcoming: MonthGroup[]
  /** Senast spelade först — det är den händelsen man tittar tillbaka på. */
  past: MonthGroup[]
}

function monthKey(kickoffUtc: string): string {
  return swedishDayKey(kickoffUtc).slice(0, 7)
}

/**
 * Grupperar händelser per månad och delar upp dem i tidigare, idag och kommande.
 *
 * Uppdelningen sker på **svenskt dygn**, inte på klockslag. En händelse i förmiddags ska
 * ligga kvar under "Idag" hela dagen — annars ser dagen tom ut på eftermiddagen.
 *
 * Månadsindelningen använder svensk tid av samma skäl som `swedishDayKey`: en händelse
 * klockan 00:30 den 1 oktober ligger i september räknat i UTC.
 */
export function groupEvents(events: TeamEvent[], now: Date | string = new Date()): GroupedEvents {
  const today: TeamEvent[] = []
  const upcoming: TeamEvent[] = []
  const past: TeamEvent[] = []

  for (const event of events) {
    const position = matchDayPosition(event.kickoffUtc, now)

    if (position === 'today') today.push(event)
    else if (position === 'upcoming') upcoming.push(event)
    else past.push(event)
  }

  return {
    today,
    upcoming: toMonthGroups(upcoming),
    // Omvänd ordning: den senast spelade händelsen är den man vill se först.
    past: toMonthGroups([...past].reverse()),
  }
}

function toMonthGroups(events: TeamEvent[]): MonthGroup[] {
  const groups: MonthGroup[] = []

  for (const event of events) {
    const key = monthKey(event.kickoffUtc)
    const last = groups.at(-1)

    if (last?.key === key) {
      last.events.push(event)
    } else {
      groups.push({ key, heading: formatMonthHeading(event.kickoffUtc), events: [event] })
    }
  }

  return groups
}
