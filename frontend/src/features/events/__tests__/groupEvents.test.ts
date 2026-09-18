import { describe, expect, it } from 'vitest'

import { groupEvents } from '@/features/events'
import type { TeamEvent } from '@/features/events'

/**
 * Grupperingen är schemalistans kärna, och den bor i en ren funktion just för att den ska
 * gå att pröva hårt utan DOM. Testerna körs i America/Los_Angeles (vitest.config.ts), så
 * ett tappat tidszonsargument faller här.
 */
function match(
  id: string,
  kickoffUtc: string,
  status: TeamEvent['status'] = 'Scheduled',
): TeamEvent {
  return {
    id,
    type: 'Match',
    kickoffUtc,
    title: null,
    opponent: `Motstandare ${id}`,
    isHome: true,
    status,
    address: 'Klarebergsvallen, Karra',
    venue: {
      name: 'Klarebergsvallen',
      address: 'Klarebergsvallen, Karra',
      latitude: 57.8,
      longitude: 12,
    },
  }
}

const now = '2026-09-15T09:00:00Z'

describe('groupEvents — uppdelning', () => {
  it('delar upp i tidigare, idag och kommande', () => {
    const result = groupEvents(
      [
        match('a', '2026-08-29T12:30:00Z'),
        match('b', '2026-09-15T16:00:00Z'),
        match('c', '2026-10-03T11:00:00Z'),
      ],
      now,
    )

    expect(result.past.flatMap((g) => g.events).map((m) => m.id)).toEqual(['a'])
    expect(result.today.map((m) => m.id)).toEqual(['b'])
    expect(result.upcoming.flatMap((g) => g.events).map((m) => m.id)).toEqual(['c'])
  })

  it('behåller en händelse som redan spelats idag under Idag', () => {
    // Annars ser dagen tom ut på eftermiddagen, precis när föräldrar tittar efter
    // resultatet.
    const result = groupEvents([match('a', '2026-09-15T07:00:00Z')], '2026-09-15T20:00:00Z')

    expect(result.today.map((m) => m.id)).toEqual(['a'])
    expect(result.past).toEqual([])
  })

  it('ger tomma grupper för ett lag utan händelser', () => {
    const result = groupEvents([], now)

    expect(result).toEqual({ today: [], upcoming: [], past: [] })
  })
})

describe('groupEvents — månadsindelning', () => {
  it('grupperar per månad med svensk rubrik', () => {
    const result = groupEvents(
      [
        match('a', '2026-09-20T12:00:00Z'),
        match('b', '2026-09-27T12:00:00Z'),
        match('c', '2026-10-04T12:00:00Z'),
      ],
      now,
    )

    expect(result.upcoming.map((g) => g.heading)).toEqual(['September 2026', 'Oktober 2026'])
    expect(result.upcoming[0]?.events).toHaveLength(2)
    expect(result.upcoming[1]?.events).toHaveLength(1)
  })

  it('använder svenskt dygn vid månadsskifte, inte UTC', () => {
    // 30 september 22:30 UTC är redan 1 oktober i Sverige. Händelsen hör hemma under
    // oktoberrubriken.
    const result = groupEvents([match('a', '2026-09-30T22:30:00Z')], now)

    expect(result.upcoming.map((g) => g.heading)).toEqual(['Oktober 2026'])
  })

  it('ger varje månadsgrupp en stabil nyckel', () => {
    const result = groupEvents([match('a', '2026-10-04T12:00:00Z')], now)

    expect(result.upcoming[0]?.key).toBe('2026-10')
  })
})

describe('groupEvents — tidigare händelser', () => {
  it('visar senast spelade först', () => {
    // Den senast spelade händelsen är den man tittar tillbaka på, inte säsongens första.
    const result = groupEvents(
      [
        match('aug', '2026-08-15T12:00:00Z'),
        match('sep-tidig', '2026-09-05T12:00:00Z'),
        match('sep-sen', '2026-09-12T12:00:00Z'),
      ],
      now,
    )

    expect(result.past.flatMap((g) => g.events).map((m) => m.id)).toEqual([
      'sep-sen',
      'sep-tidig',
      'aug',
    ])
    expect(result.past.map((g) => g.heading)).toEqual(['September 2026', 'Augusti 2026'])
  })
})

describe('groupEvents — sommartidsskiftet', () => {
  it('placerar händelser rätt över skiftet i oktober', () => {
    // Skiftdygnet är 25 timmar långt. Räknat i timmar hade uppdelningen kunnat glida.
    const result = groupEvents(
      [match('fore', '2026-10-24T10:00:00Z'), match('efter', '2026-10-25T13:30:00Z')],
      '2026-10-24T18:00:00Z',
    )

    expect(result.today.map((m) => m.id)).toEqual(['fore'])
    expect(result.upcoming.flatMap((g) => g.events).map((m) => m.id)).toEqual(['efter'])
  })

  it('behandlar en händelse strax efter midnatt svensk tid som nästa dag', () => {
    // 24 oktober 22:30 UTC är 25 oktober 00:30 i Sverige.
    const result = groupEvents([match('a', '2026-10-24T22:30:00Z')], '2026-10-24T18:00:00Z')

    expect(result.upcoming.flatMap((g) => g.events).map((m) => m.id)).toEqual(['a'])
    expect(result.today).toEqual([])
  })
})
