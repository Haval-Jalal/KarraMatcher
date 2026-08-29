import { formatMonthHeading, matchDayPosition, swedishDayKey } from '@/lib/time'

import type { Match } from './types'

export interface MonthGroup {
  /** Stabil nyckel för React, t.ex. `2026-10`. */
  key: string
  /** Rubriken som visas, t.ex. `Oktober 2026`. */
  heading: string
  matches: Match[]
}

export interface GroupedMatches {
  today: Match[]
  upcoming: MonthGroup[]
  /** Senast spelade först — det är den matchen man tittar tillbaka på. */
  past: MonthGroup[]
}

function monthKey(kickoffUtc: string): string {
  return swedishDayKey(kickoffUtc).slice(0, 7)
}

/**
 * Grupperar matcher per månad och delar upp dem i tidigare, idag och kommande.
 *
 * Uppdelningen sker på **svenskt dygn**, inte på klockslag. En match som spelades i
 * förmiddags ska ligga kvar under "Idag" hela dagen — annars ser dagen tom ut på
 * eftermiddagen, precis när föräldrar tittar efter resultatet.
 *
 * Månadsindelningen använder svensk tid av samma skäl som `swedishDayKey`: en match
 * klockan 00:30 den 1 oktober ligger i september räknat i UTC.
 */
export function groupMatches(matches: Match[], now: Date | string = new Date()): GroupedMatches {
  const today: Match[] = []
  const upcoming: Match[] = []
  const past: Match[] = []

  for (const match of matches) {
    const position = matchDayPosition(match.kickoffUtc, now)

    if (position === 'today') today.push(match)
    else if (position === 'upcoming') upcoming.push(match)
    else past.push(match)
  }

  return {
    today,
    upcoming: toMonthGroups(upcoming),
    // Omvänd ordning: den senast spelade matchen är den man vill se först när man
    // fäller ut historiken, inte säsongens allra första.
    past: toMonthGroups([...past].reverse()),
  }
}

function toMonthGroups(matches: Match[]): MonthGroup[] {
  const groups: MonthGroup[] = []

  for (const match of matches) {
    const key = monthKey(match.kickoffUtc)
    const last = groups.at(-1)

    if (last?.key === key) {
      last.matches.push(match)
    } else {
      groups.push({ key, heading: formatMonthHeading(match.kickoffUtc), matches: [match] })
    }
  }

  return groups
}
