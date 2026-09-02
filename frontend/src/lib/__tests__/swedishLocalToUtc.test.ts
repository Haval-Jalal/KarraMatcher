import { describe, expect, it } from 'vitest'

import { swedishLocalToUtc, utcToSwedishLocalInput } from '@/lib/time'

/**
 * Svensk lokaltid in, UTC ut (§KM.5).
 *
 * <h3>Varför det här testet finns</h3>
 *
 * Tränaren skriver "14:00" och menar 14:00 på planen. Sparas det som 14:00 UTC blir
 * matchen två timmar fel på sommaren — och felet syns först i föräldrarnas kalendrar,
 * alltså när det redan är för sent.
 *
 * Testerna körs i America/Los_Angeles (se `vitest.config.ts`), just för att fånga koden som
 * råkar använda webbläsarens egen zon. En tränare kan mycket väl lägga in en match från en
 * semester i Spanien.
 */

describe('normaltid, vintern', () => {
  it('räknar 14:00 svensk tid till 13:00 UTC', () => {
    // CET är UTC+1.
    expect(swedishLocalToUtc('2026-11-14T14:00')).toBe('2026-11-14T13:00:00.000Z')
  })
})

describe('sommartid, säsongen', () => {
  it('räknar 14:00 svensk tid till 12:00 UTC', () => {
    // CEST är UTC+2. Det här är den vanligaste matchen i hela appen.
    expect(swedishLocalToUtc('2026-09-20T14:00')).toBe('2026-09-20T12:00:00.000Z')
  })
})

describe('sommartidsskiftet i oktober', () => {
  /*
   * Sista sondagen i oktober 2026 ar den 25:e. Klockan stalls tillbaka klockan 03:00
   * svensk tid, alltsa 01:00 UTC. Sasongen strackier sig hit, sa det har ar inget
   * hypotetiskt fall.
   */

  it('räknar rätt kvällen före skiftet', () => {
    expect(swedishLocalToUtc('2026-10-24T14:00')).toBe('2026-10-24T12:00:00.000Z')
  })

  it('räknar rätt dagen efter skiftet', () => {
    expect(swedishLocalToUtc('2026-10-26T14:00')).toBe('2026-10-26T13:00:00.000Z')
  })

  it('räknar rätt på skiftdagen, efter omställningen', () => {
    // 14:00 den 25:e ligger efter 03:00, alltså redan i normaltid.
    expect(swedishLocalToUtc('2026-10-25T14:00')).toBe('2026-10-25T13:00:00.000Z')
  })

  it('väljer det tidigare ögonblicket när klockslaget inträffar två gånger', () => {
    /*
     * 02:00 den 25:e finns tva ganger: forst som sommartid (00:00 UTC), sedan som
     * normaltid (01:00 UTC). Vi valjer det tidigare.
     *
     * Valet spelar ingen roll for en fotbollsmatch -- ingen sparkar igang 02:00 -- men en
     * funktion som svarar olika beroende pa hur en slinga rakar konvergera ar inte en
     * funktion man litar pa.
     */
    expect(swedishLocalToUtc('2026-10-25T02:00')).toBe('2026-10-25T00:00:00.000Z')
  })
})

describe('vårskiftet, när klockan går fram', () => {
  /*
   * Sista sondagen i mars 2027 ar den 28:e. Klockan gar fran 02:00 CET till 03:00 CEST,
   * alltsa vid 01:00 UTC.
   *
   * 01:30 lokal tid ar fallet som kraver att korrigeringen gors *tva* ganger. Forsta
   * gissningen mater offseten pa fel sida om skiftet och hamnar en timme fel -- pa
   * foregaende dygn dessutom. Utan andra rundan svarar funktionen 2027-03-27T23:30Z.
   *
   * Testet hittades genom att ta bort andra rundan och se att ingenting foll. Att en
   * kodrad ingen test tacker ar antingen odod eller otestad, och har var den otestad.
   */
  it('räknar rätt timmen före skiftet', () => {
    expect(swedishLocalToUtc('2027-03-28T01:30')).toBe('2027-03-28T00:30:00.000Z')
  })

  it('räknar rätt timmen efter skiftet', () => {
    expect(swedishLocalToUtc('2027-03-28T03:30')).toBe('2027-03-28T01:30:00.000Z')
  })
})

describe('tillbaka igen', () => {
  it('ger samma tid som skrevs in', () => {
    // När tränaren öppnar en match för att ändra den ska fältet visa den tid hen skrev,
    // inte den UTC vi lagrat.
    const written = '2026-09-20T14:00'

    expect(utcToSwedishLocalInput(swedishLocalToUtc(written)!)).toBe(written)
  })

  it('håller över skiftet', () => {
    const written = '2026-10-25T02:00'

    expect(utcToSwedishLocalInput(swedishLocalToUtc(written)!)).toBe(written)
  })
})

describe('trasig indata', () => {
  it.each(['', 'i morgon', '2026-09-20', '2026-13-45T99:99'])('avvisar %s', (value) => {
    /*
     * Formularet validerar ocksa, men funktionen ska inte hitta pa en tid at nagon.
     *
     * "2026-13-45T99:99" ar det lomska fallet: siffrorna matchar monstret, och Date.UTC
     * rullar tyst over till februari aret darpa. Utan en kontroll av att resultatet
     * faktiskt visar den efterfragade tiden hade det blivit en match i fel ar.
     */
    expect(swedishLocalToUtc(value)).toBeNull()
  })

  it('avvisar timmen som hoppas över när klockan ställs fram', () => {
    // Sista sondagen i mars 2027 ar den 28:e: klockan gar fran 02:00 till 03:00, sa
    // 02:30 finns inte. Samma kontroll som fangar skrapdatum fangar den har.
    expect(swedishLocalToUtc('2027-03-28T02:30')).toBeNull()
  })
})
