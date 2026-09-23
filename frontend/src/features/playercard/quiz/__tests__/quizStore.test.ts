import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { readBestScore, recordScore } from '../quizStore'

// Källan läses som text för kopplingskontrollen, inte för beteendet.
import STORE_SOURCE from '../quizStore.ts?raw'

/**
 * Sportquizets rekord på enheten (`#206`, §KM.2).
 *
 * <para>
 * Rekordet är trivialt, men lagringen ska ändå aldrig gå ut på nätet och aldrig krascha
 * sidan. Testerna vaktar båda: ingen väg till API-lagret, och en trasig eller avstängd
 * lagring ger 0 i stället för ett fel.
 * </para>
 */

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ingenting härifrån går ut på nätet', () => {
  it('importerar inte API-lagret', () => {
    expect(STORE_SOURCE).not.toContain('@/lib/api')
    expect(STORE_SOURCE).not.toContain('fetch(')
  })
})

describe('rekordet', () => {
  it('är 0 innan man spelat', () => {
    expect(readBestScore()).toBe(0)
  })

  it('sparas när det slår det gamla', () => {
    expect(recordScore(3)).toBe(3)
    expect(readBestScore()).toBe(3)
  })

  it('står kvar när ett sämre resultat rapporteras', () => {
    recordScore(4)

    expect(recordScore(2)).toBe(4)
    expect(readBestScore()).toBe(4)
  })

  it('höjs när ett bättre resultat rapporteras', () => {
    recordScore(3)

    expect(recordScore(5)).toBe(5)
    expect(readBestScore()).toBe(5)
  })
})

describe('lagringen får aldrig krascha sidan', () => {
  it.each(['inte json', '{', 'null', '{"bestScore":"fem"}'])('ger 0 för trasig data: %s', (raw) => {
    localStorage.setItem('karra.sportquiz', raw)

    expect(readBestScore()).toBe(0)
  })

  it('ger 0 och kastar inte när lagringen är avstängd', () => {
    vi.stubGlobal('localStorage', undefined)

    expect(readBestScore()).toBe(0)
    expect(() => recordScore(5)).not.toThrow()
  })

  it('behåller det gamla rekordet när skrivningen misslyckas', () => {
    vi.stubGlobal('localStorage', {
      getItem: () => JSON.stringify({ bestScore: 3 }),
      setItem: () => {
        throw new Error('QuotaExceededError')
      },
      removeItem: () => undefined,
    })

    expect(recordScore(5)).toBe(3)
  })
})
