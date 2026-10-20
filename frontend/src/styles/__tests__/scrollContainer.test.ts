import { describe, expect, it } from 'vitest'

import CSS_SOURCE from '@/styles/index.css?raw'

/**
 * Breda tabeller (säsongsöversikten, schema-importen) wrappas i <div className="scroll">.
 *
 * Utan en `.scroll`-regel med `overflow-x` inneslöts ingenting, så en bred tabell tryckte hela
 * sidan i sidled på en telefon — precis det projektets responsiv-regel förbjuder (#593). Klassen
 * refererades men saknades en gång; testet läser den levererade stilmallen och fäller bygget om
 * regeln försvinner igen.
 */
describe('.scroll — horisontell scroll-container (#593)', () => {
  it('finns och sätter overflow-x så tabellen skrollar inom sin ruta, inte sidan', () => {
    const match = /\.scroll\s*\{([^}]*)\}/.exec(CSS_SOURCE)

    expect(match, '.scroll-regeln saknas i index.css').not.toBeNull()
    expect(match?.[1]).toMatch(/overflow-x:\s*auto/)
  })
})
