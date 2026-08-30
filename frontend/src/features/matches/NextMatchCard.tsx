import { Link } from '@tanstack/react-router'

import { formatKickoffTime, formatMatchDate, relativeDayLabel } from '@/lib/time'

import type { Match } from './types'

/**
 * Nästa match, framhävd överst.
 *
 * Det här är den enda information de flesta föräldrar öppnar appen för, så den ska synas
 * utan att någon letar.
 *
 * Ren presentation: vilken match som är nästa avgörs av `selectNextMatch` i sektionen
 * ovanför. Att välja på två ställen hade kunnat ge kortet och listan olika uppfattning om
 * vad "nästa match" är — och det är precis den oenigheten som gjorde att matchen visades
 * två gånger.
 */
export function NextMatchCard({ match, now }: { match: Match; now?: Date | string }) {
  return (
    <section className="next-match" aria-labelledby="nasta-match">
      <h2 id="nasta-match" className="next-match__label">
        Nästa match
      </h2>

      <p className="next-match__when">
        <span className="next-match__relative">{relativeDayLabel(match.kickoffUtc, now)}</span>
        <span className="next-match__time">{formatKickoffTime(match.kickoffUtc)}</span>
      </p>

      <p className="next-match__date">{formatMatchDate(match.kickoffUtc)}</p>

      <p className="next-match__opponent">
        {match.isHome ? 'Hemma mot ' : 'Borta mot '}
        <strong>{match.opponent}</strong>
      </p>

      <p className="next-match__venue">
        {match.venue.name}
        {match.address ? `, ${match.address}` : ''}
      </p>

      <p className="next-match__more">
        <Link to="/match/$id" params={{ id: match.id }}>
          Visa matchen
        </Link>
      </p>
    </section>
  )
}
