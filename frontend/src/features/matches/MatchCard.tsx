import { Link } from '@tanstack/react-router'

import { formatKickoffTime, formatMatchDate } from '@/lib/time'

import type { Match } from './types'

/**
 * En match i listan.
 *
 * Inställd match märks med **text** och inte bara med en färg eller en överstrykning —
 * en förälder som inte skiljer färger ska förstå att matchen är inställd (WCAG 1.4.1).
 * Märkningen står först i rubriken, så att en skärmläsare säger det innan motståndaren.
 *
 * Hela kortet är en länk till matchdetaljen. Det ger en stor träffyta på en telefon, och
 * länkens namn blir kortets hela innehåll — alltså tid, datum, motståndare och plats,
 * vilket är precis vad en skärmläsare behöver för att skilja länkarna åt i en lista.
 */
export function MatchCard({ match }: { match: Match }) {
  const isCancelled = match.status === 'Cancelled'
  const isPostponed = match.status === 'Postponed'

  return (
    <li>
      <Link to="/match/$id" params={{ id: match.id }} className="match-card">
        <p className="match-card__when">
          <span className="match-card__time">{formatKickoffTime(match.kickoffUtc)}</span>
          <span className="match-card__date">{formatMatchDate(match.kickoffUtc)}</span>
        </p>

        <p className="match-card__opponent">
          {isCancelled && <span className="badge badge--cancelled">Inställd</span>}
          {isPostponed && <span className="badge">Framflyttad</span>}
          <span>{match.isHome ? 'Hemma mot' : 'Borta mot'} </span>
          <strong>{match.opponent}</strong>
        </p>

        <p className="match-card__venue">
          {match.venue.name}
          {match.address ? `, ${match.address}` : ''}
        </p>
      </Link>
    </li>
  )
}
