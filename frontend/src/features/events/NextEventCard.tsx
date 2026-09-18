import { Link } from '@tanstack/react-router'

import { formatKickoffTime, formatMatchDate, relativeDayLabel } from '@/lib/time'

import { eventTypeLabel, type TeamEvent } from './types'

/**
 * Nästa händelse, framhävd överst.
 *
 * Det här är den enda information de flesta föräldrar öppnar appen för, så den ska synas
 * utan att någon letar.
 *
 * Ren presentation: vilken händelse som är nästa avgörs av `selectNextEvent` i sektionen
 * ovanför.
 */
export function NextEventCard({ event, now }: { event: TeamEvent; now?: Date | string }) {
  const isMatch = event.type === 'Match'

  return (
    <section className="next-match" aria-labelledby="nasta-handelse">
      <h2 id="nasta-handelse" className="next-match__label">
        {isMatch ? 'Nästa match' : 'Nästa händelse'}
      </h2>

      <p className="next-match__when">
        <span className="next-match__relative">{relativeDayLabel(event.kickoffUtc, now)}</span>
        <span className="next-match__time">{formatKickoffTime(event.kickoffUtc)}</span>
      </p>

      <p className="next-match__date">{formatMatchDate(event.kickoffUtc)}</p>

      <p className="next-match__opponent">
        {isMatch ? (
          <>
            {event.isHome ? 'Hemma mot ' : 'Borta mot '}
            <strong>{event.opponent}</strong>
          </>
        ) : (
          <>
            {eventTypeLabel(event.type)}: <strong>{event.title}</strong>
          </>
        )}
      </p>

      <p className="next-match__venue">
        {event.venue.name}
        {event.address ? `, ${event.address}` : ''}
      </p>

      <p className="next-match__more">
        <Link to="/handelse/$id" params={{ id: event.id }}>
          {isMatch ? 'Visa matchen' : 'Visa händelsen'}
        </Link>
      </p>
    </section>
  )
}
