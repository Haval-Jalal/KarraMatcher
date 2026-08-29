import { formatKickoffTime, formatMatchDate, relativeDayLabel } from '@/lib/time'

import { selectNextMatch } from './selectNextMatch'
import type { Match } from './types'

/**
 * Nästa match, framhävd överst.
 *
 * Det här är den enda information de flesta föräldrar öppnar appen för, så den ska synas
 * utan att någon letar. Kortet renderar ingenting när säsongen är slut — matchlistan säger
 * redan det, och två meddelanden om samma sak är sämre än ett.
 */
export function NextMatchCard({ matches, now }: { matches: Match[]; now?: Date | string }) {
  const match: Match | null = selectNextMatch(matches, now)

  if (match === null) {
    return null
  }

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
    </section>
  )
}
