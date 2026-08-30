import { describe, expect, it } from 'vitest'

import { detectMapsPlatform, directionsDestination, directionsUrl } from '@/lib/maps'

describe('detectMapsPlatform', () => {
  it.each([
    ['iPhone', 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15'],
    ['iPad (äldre)', 'Mozilla/5.0 (iPad; CPU OS 12_0 like Mac OS X) AppleWebKit/605.1.15'],
    ['Mac', 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36'],
  ])('väljer Apple Maps på %s', (_name, agent) => {
    expect(detectMapsPlatform(agent)).toBe('apple')
  })

  it('väljer Apple Maps på iPadOS, som uppger sig vara Macintosh', () => {
    // iPadOS 13 och senare skickar en Macintosh-agent. Det gör ingen skada här: Apple
    // Maps är rätt svar för både iPad och Mac.
    const ipadOs =
      'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15'

    expect(detectMapsPlatform(ipadOs)).toBe('apple')
  })

  it.each([
    ['Android', 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36'],
    ['Windows', 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'],
    ['Linux', 'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36'],
  ])('väljer Google Maps på %s', (_name, agent) => {
    expect(detectMapsPlatform(agent)).toBe('google')
  })

  it('väljer Google Maps när enheten är okänd', () => {
    // Google Maps fungerar i en webbläsare på allt. Att gissa Apple hade kunnat skicka en
    // Android-användare till en sida som inte öppnar deras kartapp.
    expect(detectMapsPlatform('')).toBe('google')
  })
})

describe('directionsDestination', () => {
  it('använder adressen när den finns', () => {
    expect(directionsDestination('Kareby Hed 11', 'Kareby Hed, Kareby, Kungälv')).toBe(
      'Kareby Hed, Kareby, Kungälv',
    )
  })

  it.each([[null], [undefined], [''], ['   ']])(
    'faller tillbaka på spelplatsens namn när adressen är %j',
    (address) => {
      // En tränare kan lägga in en plan som bara har ett namn. "Kareby Hed" är sökbart;
      // en tom sträng är det inte.
      expect(directionsDestination('Kareby Hed 11', address)).toBe('Kareby Hed 11')
    },
  )
})

describe('directionsUrl', () => {
  it('bygger en Apple Maps-länk med destinationen', () => {
    expect(directionsUrl('Kareby Hed, Kungälv', 'apple')).toBe(
      'https://maps.apple.com/?daddr=Kareby%20Hed%2C%20Kung%C3%A4lv',
    )
  })

  it('bygger en Google Maps-länk med destinationen', () => {
    expect(directionsUrl('Kareby Hed, Kungälv', 'google')).toBe(
      'https://www.google.com/maps/dir/?api=1&destination=Kareby%20Hed%2C%20Kung%C3%A4lv',
    )
  })

  it.each([
    ['Kärra & Göteborg'],
    ['Plan #3, Öckerö'],
    ['Väg 155 / Hjuvik'],
    ['Prästängen?ort=Öckerö'],
  ])('URL-kodar adressen %s så att den inte bryter länken', (address) => {
    // Svenska tecken och skiljetecken i adresser är regel, inte undantag. En okodad
    // ampersand eller ett frågetecken hade kapat destinationen på mitten.
    const url = directionsUrl(address, 'google')

    expect(url).toContain(encodeURIComponent(address))
    expect(url.split('destination=')[1]).toBe(encodeURIComponent(address))
  })

  it('lämnar inga råa specialtecken kvar i frågesträngen', () => {
    const url = directionsUrl('A & B ? C # D', 'apple')
    const query = url.slice(url.indexOf('?') + 1)

    expect(query.split('daddr=')[1]).not.toMatch(/[&?#\s]/)
  })
})
