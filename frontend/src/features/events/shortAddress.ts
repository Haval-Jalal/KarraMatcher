/**
 * Kortar en geokodad adress till något läsbart (`#redesign`).
 *
 * Nominatim ger tillbaka hela raden — *"Klarebergsvallen 3, Klarebergsvallen, von Seths Gata,
 * Larsered, Kärra, Hisingen, Mysterna, Göteborgs Stad, Västra Götalands län, 425 32, Sverige"* —
 * vilket är en vägg av text i ett kort. Föräldern behöver bara veta *var*: platsen och, om det
 * tillför något, orten.
 *
 * Regeln är enkel och förutsägbar (en gissning som tappar orten vore värre än att visa lite mindre):
 * första delen (planen/gatan), plus andra delen om den lägger till något — inte om den bara upprepar
 * platsen eller är brus (postnummer, län, land). Aldrig mer än två delar.
 */
const NOISE = /^\d{3}\s?\d{2}$|län$|^sverige$|^sweden$/i

export function shortAddress(address: string): string {
  const parts = address
    .split(',')
    .map((part) => part.trim())
    .filter(Boolean)

  const first = parts[0]
  if (first === undefined) {
    return address
  }
  if (parts.length === 1) {
    return first
  }

  const second = parts[1]!
  const redundant = first.toLowerCase().startsWith(second.toLowerCase()) || NOISE.test(second)

  return redundant ? first : `${first}, ${second}`
}

/**
 * Platsraden i ett kort: spelplatsens namn plus den korta adressen — men utan att upprepa sig.
 * När adressen redan börjar med platsens namn (hemmaplanen heter ofta som gatan) visas bara
 * adressen. Saknas adress visas bara namnet.
 */
export function venueLine(venueName: string, address: string): string {
  const short = address.trim() === '' ? '' : shortAddress(address)

  if (short === '') {
    return venueName
  }
  if (venueName === '' || short.toLowerCase().startsWith(venueName.toLowerCase())) {
    return short
  }

  return `${venueName}, ${short}`
}
