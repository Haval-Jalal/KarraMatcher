/**
 * Kartlänkar till spelplatsen.
 *
 * Bortamatcher spelas på planer ingen hittar till, så det här är den funktion som konkret
 * hindrar folk från att komma fel. Den måste därför peka rätt — och peka rätt i den
 * kartapp föräldern faktiskt har.
 *
 * <h3>Varför adressen och inte koordinaterna</h3>
 *
 * Spelplatserna har koordinater i databasen, men de är avrundade till två decimaler,
 * alltså ungefär **1,1 kilometers** precision. Att navigera dit hade kunnat lämna någon en
 * kilometer fel, utan något sätt att märka det — kartappen visar bara en namnlös nål.
 * Adressen låter i stället kartappen visa ett namngivet mål som går att känna igen.
 *
 * Koordinaterna duger utmärkt till väderprognosen (#22), där en kilometer inte spelar
 * någon roll. Ska de någon gång driva navigation måste de först bli exakta.
 */

export type MapsPlatform = 'apple' | 'google'

/**
 * Vilken kartapp enheten sannolikt har.
 *
 * iPadOS 13 och senare uppger sig vara "Macintosh", vilket inte är ett problem här:
 * Apple Maps är rätt svar för både iPad och Mac.
 */
export function detectMapsPlatform(userAgent?: string): MapsPlatform {
  const agent = userAgent ?? globalThis.navigator?.userAgent ?? ''

  return /iPhone|iPad|iPod|Macintosh/i.test(agent) ? 'apple' : 'google'
}

/**
 * Vad kartan ska söka efter: adressen om den finns, annars spelplatsens namn.
 *
 * En spelplats utan adress är inte hypotetisk — en tränare kan lägga in en plan som bara
 * har ett namn. "Kareby Hed" är sökbart; en tom sträng är det inte.
 */
export function directionsDestination(venueName: string, address?: string | null): string {
  const trimmed = address?.trim()

  return trimmed !== undefined && trimmed !== '' ? trimmed : venueName
}

/**
 * Länk som öppnar vägbeskrivning till målet.
 *
 * Båda adresserna är dokumenterade webbadresser som fungerar även utan appen installerad:
 * på en dator öppnas maps.apple.com respektive google.com/maps i webbläsaren.
 *
 * <h3>Färdsättet anges uttryckligen</h3>
 *
 * Utan `dirflg` respektive `travelmode` använder kartappen det färdsätt användaren senast
 * råkade välja. Stod den i kollektivtrafik får föräldern en resa till närmaste hållplats i
 * stället för en bilväg till planen — vilket ser ut som att adressen är fel, fast
 * adressträngen är riktig.
 *
 * Bil är rätt förvalt här: en förälder med barn, matchkläder och ofta en boll i bagaget
 * kör till bortamatchen. Den som ändå åker kollektivt byter i kartappen med ett tryck.
 */
export function directionsUrl(destination: string, platform: MapsPlatform): string {
  const encoded = encodeURIComponent(destination)

  return platform === 'apple'
    ? `https://maps.apple.com/?daddr=${encoded}&dirflg=d`
    : `https://www.google.com/maps/dir/?api=1&destination=${encoded}&travelmode=driving`
}
