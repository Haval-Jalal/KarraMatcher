import type { TeamEvent } from './types'

/**
 * Knapp som laddar ner händelsen som kalenderfil.
 *
 * För den som vill lägga in en enstaka händelse utan att prenumerera på hela schemat.
 *
 * Adressen ligger utanför `/api` och proxas av samma Vercel-rewrite som lagets feed, så
 * länken fungerar från appens egen domän utan att Render-URL:en syns någonstans (§KM.11).
 */
export function CalendarLink({ event }: { event: TeamEvent }) {
  return (
    <a className="button button--action" href={`/calendar/handelse/${event.id}.ics`} download>
      Lägg till i kalendern
      <span className="visually-hidden"> — laddar ner händelsen som kalenderfil</span>
    </a>
  )
}
