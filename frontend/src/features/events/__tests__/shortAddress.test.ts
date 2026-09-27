import { describe, expect, it } from 'vitest'

import { shortAddress, venueLine } from '../shortAddress'

const LONG =
  'Klarebergsvallen 3, Klarebergsvallen, von Seths Gata, Larsered, Kärra, Hisingen, ' +
  'Mysterna, Göteborgs Stad, Västra Götalands län, 425 32, Sverige'

describe('shortAddress', () => {
  it('kortar en lång Nominatim-rad till platsen (nästa del upprepar den)', () => {
    // Andra delen "Klarebergsvallen" är en upprepning av platsen → bara platsen visas.
    expect(shortAddress(LONG)).toBe('Klarebergsvallen 3')
  })

  it('behåller orten när den tillför något', () => {
    expect(shortAddress('Storgatan 5, Kungälv, Sverige')).toBe('Storgatan 5, Kungälv')
  })

  it('lämnar en redan kort adress', () => {
    expect(shortAddress('Prästängen 31, Öckerö')).toBe('Prästängen 31, Öckerö')
  })

  it('sållar bort brus (postnummer, län, land) som andra del', () => {
    expect(shortAddress('Bortavägen 5, 425 32, Sverige')).toBe('Bortavägen 5')
  })
})

describe('venueLine', () => {
  it('visar bara adressen när platsnamnet upprepas', () => {
    expect(venueLine('Klarebergsvallen', LONG)).toBe('Klarebergsvallen 3')
  })

  it('kombinerar namn och adress när namnet tillför något', () => {
    expect(venueLine('Kärra IP', 'Storgatan 5, Kungälv, Sverige')).toBe(
      'Kärra IP, Storgatan 5, Kungälv',
    )
  })

  it('visar bara namnet när adressen saknas', () => {
    expect(venueLine('Kärra IP', '')).toBe('Kärra IP')
  })
})
