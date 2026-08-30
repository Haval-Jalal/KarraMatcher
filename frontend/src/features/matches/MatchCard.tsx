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
 * Den ordningen är prövad med VoiceOver och TalkBack (`#28`) och ska inte kastas om.
 *
 * <h3>Kanten till vänster</h3>
 *
 * Hemma och borta ska gå att skilja åt utan att läsa (`#116`). Kanten är därför fylld
 * vid hemmamatch och streckad vid bortamatch — två skillnader, inte bara en färg, och
 * texten "Hemma mot" står kvar (WCAG 1.4.1). Är matchen inställd tar den märkningen
 * över kanten helt: då är det den enda upplysning som spelar någon roll.
 */
export function MatchCard({ match }: { match: Match }) {
  const isCancelled = match.status === 'Cancelled'
  const isPostponed = match.status === 'Postponed'

  const className = [
    'match-card',
    match.isHome ? 'match-card--home' : 'match-card--away',
    isCancelled ? 'match-card--cancelled' : '',
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <li>
      <Link to="/match/$id" params={{ id: match.id }} className={className}>
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
