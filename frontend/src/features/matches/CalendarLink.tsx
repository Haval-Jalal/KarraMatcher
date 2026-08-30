import type { Match } from './types'

/**
 * Knapp som laddar ner matchen som kalenderfil.
 *
 * För den som vill lägga in en enstaka match utan att prenumerera på hela schemat — en
 * morförälder som ska på en match, eller en förälder som redan har kalendern full.
 *
 * Adressen ligger utanför `/api` och proxas av samma Vercel-rewrite som lagets feed, så
 * länken fungerar från appens egen domän utan att Render-URL:en syns någonstans (§KM.11).
 */
export function CalendarLink({ match }: { match: Match }) {
  return (
    <a className="button button--action" href={`/calendar/match/${match.id}.ics`} download>
      Lägg till i kalendern
      <span className="visually-hidden"> — laddar ner matchen som kalenderfil</span>
    </a>
  )
}
