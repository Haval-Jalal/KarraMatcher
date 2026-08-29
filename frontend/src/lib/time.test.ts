import { describe, expect, it } from 'vitest'

import {
  SWEDISH_TIME_ZONE,
  formatDayAndMonth,
  formatKickoffTime,
  formatMatchDate,
  formatMonthHeading,
  matchDayPosition,
  relativeDayLabel,
  swedishDayDifference,
  swedishDayKey,
} from '@/lib/time'

/**
 * Testerna körs i America/Los_Angeles (se vitest.config.ts). Det är avsiktligt: om någon
 * funktion råkar använda maskinens lokaltid i stället för Europe/Stockholm faller testet
 * här i stället för att upptäckas av en förälder en söndagsmorgon i oktober.
 */
describe('tidszonens förutsättningar', () => {
  it('kör i en zon med annan förskjutning än Sveriges', () => {
    /*
     * Att bara kontrollera zonens namn räcker inte. Utvecklarmaskinen kan stå i
     * Europe/Berlin, som har exakt samma förskjutningar som Stockholm året runt — då hade
     * hela den här sviten passerat av ren tur även om någon funktion tappat sitt
     * tidszonsargument. Vi jämför därför faktisk förskjutning för ett känt ögonblick.
     */
    const instant = new Date('2026-10-24T10:00:00Z')

    const swedishHour = Number(
      new Intl.DateTimeFormat('sv-SE', {
        timeZone: SWEDISH_TIME_ZONE,
        hour: '2-digit',
        hour12: false,
      }).format(instant),
    )
    const localHour = instant.getHours()

    expect(localHour).not.toBe(swedishHour)
  })
})

describe('formatKickoffTime', () => {
  it.each([
    // Sommartid, CEST (UTC+2) — hela höstsäsongens matcher ligger här.
    ['2026-08-29T12:30:00Z', '14:30'],
    ['2026-10-24T10:00:00Z', '12:00'],
    // Skiftdygnet: klockan ställs tillbaka natten till söndag 25 oktober 2026.
    ['2026-10-25T13:30:00Z', '14:30'],
    // Vintertid, CET (UTC+1).
    ['2026-11-15T13:30:00Z', '14:30'],
    // Vårskiftet 2027 — klockan ställs fram natten till söndag 28 mars.
    ['2027-03-27T12:00:00Z', '13:00'],
    ['2027-03-28T12:00:00Z', '14:00'],
  ])('%s visas som %s i svensk tid', (utc, expected) => {
    expect(formatKickoffTime(utc)).toBe(expected)
  })

  it('samma klockslag två gånger under den timme som upprepas', () => {
    // Natten till 25 oktober inträffar 02:30 två gånger: en gång i sommartid och en gång
    // i vintertid. Båda ska visas som 02:30 — det är rätt, och det är den enda gången på
    // året två olika ögonblick har samma klockslag.
    expect(formatKickoffTime('2026-10-25T00:30:00Z')).toBe('02:30')
    expect(formatKickoffTime('2026-10-25T01:30:00Z')).toBe('02:30')
  })

  it('kastar på en trasig tidsstämpel i stället för att visa 1970', () => {
    // En felaktig tid som tyst blir "1 januari 1970" ser rimlig ut i en lista. Ett fel
    // som syns är bättre än ett fel som ser ut som data.
    expect(() => formatKickoffTime('inte-en-tid')).toThrow(TypeError)
  })
})

describe('formatMatchDate och formatMonthHeading', () => {
  it('ger veckodag och datum med versal', () => {
    expect(formatMatchDate('2026-10-24T10:00:00Z')).toBe('Lördag 24 oktober')
  })

  it('ger månadsrubrik med versal', () => {
    expect(formatMonthHeading('2026-10-24T10:00:00Z')).toBe('Oktober 2026')
  })

  it('använder svenskt dygn vid månadsskifte, inte UTC', () => {
    // 30 september 22:30 UTC är redan 1 oktober i Sverige. Matchen hör hemma under
    // oktoberrubriken, annars hamnar den under fel månad i listan.
    expect(formatMonthHeading('2026-09-30T22:30:00Z')).toBe('Oktober 2026')
    expect(formatDayAndMonth('2026-09-30T22:30:00Z')).toBe('1 oktober')
  })
})

describe('swedishDayKey', () => {
  it('grupperar på svenskt dygn och inte på UTC-dygn', () => {
    // 24 oktober 22:30 UTC är 25 oktober 00:30 i Sverige. Grupperas den på UTC hamnar
    // matchen under gårdagens datumrubrik.
    expect(swedishDayKey('2026-10-24T22:30:00Z')).toBe('2026-10-25')
    expect(swedishDayKey('2026-10-24T10:00:00Z')).toBe('2026-10-24')
  })

  it('ger samma nyckel för båda halvorna av skiftdygnet', () => {
    expect(swedishDayKey('2026-10-25T00:30:00Z')).toBe('2026-10-25')
    expect(swedishDayKey('2026-10-25T23:30:00Z')).toBe('2026-10-26')
  })
})

describe('swedishDayDifference', () => {
  it('räknar kalenderdygn, inte timmar', () => {
    // Skiftdygnet är 25 timmar långt. Räknat i timmar hade lördag till söndag blivit
    // "1,04 dygn" och avrundningen hade kunnat slå fel just den helgen.
    const lordag = '2026-10-24T12:00:00Z'
    const sondag = '2026-10-25T13:00:00Z'

    expect(swedishDayDifference(sondag, lordag)).toBe(1)
    expect(swedishDayDifference(lordag, sondag)).toBe(-1)
  })

  it('ger noll för två tider samma svenska dygn', () => {
    expect(swedishDayDifference('2026-10-25T05:00:00Z', '2026-10-25T20:00:00Z')).toBe(0)
  })

  it('hanterar årsskiftet', () => {
    expect(swedishDayDifference('2027-01-01T11:00:00Z', '2026-12-31T11:00:00Z')).toBe(1)
  })
})

describe('matchDayPosition', () => {
  const idag = '2026-10-25T09:00:00Z'

  it.each([
    ['2026-10-24T09:00:00Z', 'past'],
    ['2026-10-25T18:00:00Z', 'today'],
    ['2026-10-26T09:00:00Z', 'upcoming'],
  ])('%s är %s', (kickoff, expected) => {
    expect(matchDayPosition(kickoff, idag)).toBe(expected)
  })

  it('en match tidigare idag räknas fortfarande som idag', () => {
    // Matchlistan grupperar per dygn, inte per klockslag. En avslutad förmiddagsmatch ska
    // ligga kvar under "Idag" hela dagen — annars ser dagen tom ut på eftermiddagen.
    expect(matchDayPosition('2026-10-25T07:00:00Z', '2026-10-25T20:00:00Z')).toBe('today')
  })
})

describe('relativeDayLabel', () => {
  const idag = '2026-10-25T09:00:00Z'

  it.each([
    ['2026-10-25T13:00:00Z', 'Idag'],
    ['2026-10-26T13:00:00Z', 'Imorgon'],
    ['2026-10-24T13:00:00Z', 'Igår'],
    ['2026-10-27T13:00:00Z', 'På tisdag'],
    ['2026-10-31T13:00:00Z', 'På lördag'],
  ])('%s ger %s', (kickoff, expected) => {
    expect(relativeDayLabel(kickoff, idag)).toBe(expected)
  })

  it('går över till datum när veckodagen blir tvetydig', () => {
    // Sju dagar bort säger "på söndag" inget om vilken söndag som menas.
    expect(relativeDayLabel('2026-11-01T13:00:00Z', idag)).toBe('1 november')
    expect(relativeDayLabel('2026-11-15T13:00:00Z', idag)).toBe('15 november')
  })

  it('fungerar över skiftet utan att tappa ett dygn', () => {
    // Referensen ligger före skiftet, matchen efter. Räknat i timmar hade det 25 timmar
    // långa dygnet kunnat ge "Idag" för en match som är imorgon.
    expect(relativeDayLabel('2026-10-25T13:00:00Z', '2026-10-24T13:00:00Z')).toBe('Imorgon')
  })
})
