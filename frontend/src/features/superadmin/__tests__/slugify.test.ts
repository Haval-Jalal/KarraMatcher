import { describe, expect, it } from 'vitest'

import { slugify } from '../slugify'

/**
 * Slug-förslaget (`#257`). Vaktar att resultatet alltid är en giltig slug (små a–z, siffror,
 * bindestreck) och att svenska tecken translittereras i stället för att falla bort.
 */
describe('slugify', () => {
  it.each([
    ['Fotboll', 'fotboll'],
    ['Kärra IF', 'karra-if'],
    ['P2016 Gul', 'p2016-gul'],
    ['Åre Såå', 'are-saa'],
    ['  Ada & Bea  ', 'ada-bea'],
    ['Blå-Vit', 'bla-vit'],
    ['', ''],
    ['???', ''],
  ])('gör "%s" till "%s"', (input, expected) => {
    expect(slugify(input)).toBe(expected)
  })

  it('matchar valideringsregeln för icke-tomma namn', () => {
    const rule = /^[a-z0-9]+(-[a-z0-9]+)*$/

    for (const name of ['Fotboll', 'Kärra IF', 'P2016 Gul', 'Ada & Bea']) {
      expect(slugify(name)).toMatch(rule)
    }
  })
})
