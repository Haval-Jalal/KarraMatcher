import { describe, expect, it } from 'vitest'

import { replyLabel } from '../replyLabel'

/**
 * Delad etikett för närvarosvar (`#614`). Förr fanns två kopior: admin-vyn visade "–" för ett
 * obesvarat barn, tränar-vyn "Inget svar". Nu är de en, och "Inget svar" vann.
 */
describe('replyLabel', () => {
  it('ger Ja/Nej för ett svar', () => {
    expect(replyLabel('Coming')).toBe('Ja')
    expect(replyLabel('NotComing')).toBe('Nej')
  })

  it('ger "Inget svar" när barnet inte svarat — aldrig ett ensamt bindestreck', () => {
    expect(replyLabel(null)).toBe('Inget svar')
  })
})
