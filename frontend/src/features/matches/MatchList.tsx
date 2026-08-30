import { useId, useState } from 'react'

import { MatchCard } from './MatchCard'
import { groupMatches } from './groupMatches'
import type { Match } from './types'

/**
 * Lagets matcher: dagens först, sedan kommande per månad, och tidigare hopfällda.
 *
 * Historiken är hopfälld eftersom föräldern vill se nästa match först och slippa bläddra
 * förbi halva säsongen. Knappen anger antalet, så att man vet vad som väntar bakom den
 * innan man trycker.
 */
export function MatchList({
  matches,
  now,
  excludeId,
}: {
  matches: Match[]
  now?: Date | string
  /**
   * Matchen som redan visas i kortet ovanför. Utesluts på **id** och inte som "första
   * posten" — kortet hoppar över inställda matcher, så första posten är inte alltid den
   * kortet visar, och att ta bort fel post hade dolt en inställd match.
   */
  excludeId?: string
}) {
  const [showPast, setShowPast] = useState(false)
  const pastId = useId()

  if (matches.length === 0) {
    return (
      <p className="state">
        Inga matcher är inlagda för det här laget än. Schemat läggs in inför säsongen — då dyker
        matcherna upp här av sig själva.
      </p>
    )
  }

  const shown = excludeId === undefined ? matches : matches.filter((m) => m.id !== excludeId)
  const { today, upcoming, past } = groupMatches(shown, now)
  const pastCount = past.reduce((total, group) => total + group.matches.length, 0)

  if (shown.length === 0) {
    // Kortet ovanför visar den enda match som fanns. Utan det här beskedet hade
    // rubriken "Matcher" stått ensam över tomrum och sett trasig ut.
    return <p className="state">Inga fler matcher är inlagda.</p>
  }

  // Med ett kort ovanför är en tom framtid inte "säsongen är slut" — det står redan en
  // kommande match på sidan. Beskedet gäller bara när listan är allt som finns.
  const seasonOver = today.length === 0 && upcoming.length === 0 && excludeId === undefined

  return (
    <div className="match-list">
      {today.length > 0 && (
        <section aria-labelledby="idag">
          <h3 id="idag" className="match-list__heading match-list__heading--today">
            Idag
          </h3>
          <ul className="match-list__items">
            {today.map((match) => (
              <MatchCard key={match.id} match={match} />
            ))}
          </ul>
        </section>
      )}

      {upcoming.map((group) => (
        <section key={group.key} aria-labelledby={`manad-${group.key}`}>
          <h3 id={`manad-${group.key}`} className="match-list__heading">
            {group.heading}
          </h3>
          <ul className="match-list__items">
            {group.matches.map((match) => (
              <MatchCard key={match.id} match={match} />
            ))}
          </ul>
        </section>
      ))}

      {seasonOver && <p className="state">Säsongen är slut. Inga fler matcher är inlagda.</p>}

      {pastCount > 0 && (
        <div className="match-list__past">
          <button
            type="button"
            className="button"
            aria-expanded={showPast}
            aria-controls={pastId}
            onClick={() => {
              setShowPast((open) => !open)
            }}
          >
            {showPast
              ? 'Dölj tidigare matcher'
              : `Visa ${String(pastCount)} tidigare ${pastCount === 1 ? 'match' : 'matcher'}`}
          </button>

          <div id={pastId} hidden={!showPast}>
            {past.map((group) => (
              <section key={group.key} aria-labelledby={`tidigare-${group.key}`}>
                <h3 id={`tidigare-${group.key}`} className="match-list__heading">
                  {group.heading}
                </h3>
                <ul className="match-list__items">
                  {group.matches.map((match) => (
                    <MatchCard key={match.id} match={match} />
                  ))}
                </ul>
              </section>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
