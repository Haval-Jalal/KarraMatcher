/**
 * Länk till lagets kalenderprenumeration.
 *
 * Sannolikt appens mest värdefulla funktion: prenumerera en gång, och nya matcher dyker
 * upp av sig själva medan en flyttad match flyttar sig i telefonens egen kalender. Det är
 * också fallbacken för iOS-föräldrar som inte installerar appen på hemskärmen.
 *
 * `webcal://` och inte `https://`: schemat får telefonen att erbjuda en *prenumeration* i
 * stället för att ladda ner en engångsfil. Skillnaden är hela poängen — en nedladdad fil
 * uppdateras aldrig.
 */
export function TeamCalendarLink({ slug }: { slug: string }) {
  const path = `/calendar/${slug}.ics`
  const host = globalThis.location?.host ?? ''

  return (
    <p className="calendar-subscribe">
      <a className="button button--action" href={`webcal://${host}${path}`}>
        Prenumerera i kalendern
      </a>
      <span className="calendar-subscribe__hint">
        Nya och flyttade matcher uppdateras av sig själva. Fungerar utan att appen öppnas.
      </span>
    </p>
  )
}
