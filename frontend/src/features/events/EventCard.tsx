import { Link } from '@tanstack/react-router'

import { formatKickoffTime, formatMatchDate } from '@/lib/time'

import { venueLine } from './shortAddress'
import { eventTypeLabel, type TeamEvent } from './types'

/**
 * En händelse i listan.
 *
 * Inställd händelse märks med **text** och inte bara med en färg — en förälder som inte
 * skiljer färger ska förstå att den är inställd (WCAG 1.4.1). Märkningen står först i
 * rubriken, så en skärmläsare säger det innan motståndaren/rubriken.
 *
 * Hela kortet är en länk till detaljen. Det ger en stor träffyta på en telefon, och
 * länkens namn blir kortets hela innehåll — tid, datum, motståndare/rubrik och plats.
 *
 * <h3>Kanten till vänster</h3>
 *
 * För en match ska hemma och borta gå att skilja åt utan att läsa (`#116`): kanten är
 * fylld vid hemma och streckad vid borta, och texten står kvar. Träning/övrigt saknar
 * hemma/borta och får en neutral kant.
 */
export function EventCard({ event }: { event: TeamEvent }) {
  const isCancelled = event.status === 'Cancelled'
  const isPostponed = event.status === 'Postponed'
  const isMatch = event.type === 'Match'

  const className = [
    'match-card',
    isMatch ? (event.isHome ? 'match-card--home' : 'match-card--away') : 'match-card--other',
    isCancelled ? 'match-card--cancelled' : '',
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <li>
      <Link to="/handelse/$id" params={{ id: event.id }} className={className}>
        <p className="match-card__when">
          <span className="match-card__time">{formatKickoffTime(event.kickoffUtc)}</span>
          <span className="match-card__date">{formatMatchDate(event.kickoffUtc)}</span>
        </p>

        <p className="match-card__opponent">
          {isCancelled && <span className="event-badge event-badge--cancelled">Inställd</span>}
          {isPostponed && <span className="event-badge">Framflyttad</span>}
          {isMatch ? (
            event.opponent?.trim() ? (
              <>
                <span>{event.isHome ? 'Hemma mot' : 'Borta mot'} </span>
                <strong>{event.opponent}</strong>
              </>
            ) : (
              // Utan motståndare: bara hemma/borta, inget hängande "mot" med tom strong (#490/#547).
              <strong>{event.isHome ? 'Hemma' : 'Borta'}</strong>
            )
          ) : (
            <>
              <span>{eventTypeLabel(event.type)}: </span>
              <strong>{event.title}</strong>
            </>
          )}
        </p>

        <p className="match-card__meta">
          {isMatch && (
            <span className={event.isHome ? 'pill pill--home' : 'pill pill--away'}>
              {event.isHome ? 'Hemma' : 'Borta'}
            </span>
          )}
          <span className="match-card__venue">{venueLine(event.venue.name, event.address)}</span>
        </p>
      </Link>
    </li>
  )
}
