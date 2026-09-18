import { useId, useState } from 'react'

import { EventCard } from './EventCard'
import { groupEvents } from './groupEvents'
import type { TeamEvent } from './types'

/**
 * Lagets händelser: dagens först, sedan kommande per månad, och tidigare hopfällda.
 *
 * Historiken är hopfälld eftersom föräldern vill se nästa händelse först och slippa bläddra
 * förbi halva säsongen. Knappen anger antalet, så att man vet vad som väntar bakom den.
 */
export function EventList({
  events,
  now,
  excludeId,
}: {
  events: TeamEvent[]
  now?: Date | string
  /**
   * Händelsen som redan visas i kortet ovanför. Utesluts på **id** och inte som "första
   * posten" — kortet hoppar över inställda händelser, så första posten är inte alltid den
   * kortet visar.
   */
  excludeId?: string
}) {
  const [showPast, setShowPast] = useState(false)
  const pastId = useId()

  if (events.length === 0) {
    return (
      <p className="state">
        Inga händelser är inlagda för det här laget än. Schemat läggs in inför säsongen — då dyker
        de upp här av sig själva.
      </p>
    )
  }

  const shown = excludeId === undefined ? events : events.filter((e) => e.id !== excludeId)
  const { today, upcoming, past } = groupEvents(shown, now)
  const pastCount = past.reduce((total, group) => total + group.events.length, 0)

  if (shown.length === 0) {
    // Kortet ovanför visar den enda händelse som fanns.
    return <p className="state">Inga fler händelser är inlagda.</p>
  }

  // Med ett kort ovanför är en tom framtid inte "säsongen är slut" — det står redan en
  // kommande händelse på sidan. Beskedet gäller bara när listan är allt som finns.
  const seasonOver = today.length === 0 && upcoming.length === 0 && excludeId === undefined

  return (
    <div className="match-list">
      {today.length > 0 && (
        <section aria-labelledby="idag">
          <h3 id="idag" className="match-list__heading match-list__heading--today">
            Idag
          </h3>
          <ul className="match-list__items">
            {today.map((event) => (
              <EventCard key={event.id} event={event} />
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
            {group.events.map((event) => (
              <EventCard key={event.id} event={event} />
            ))}
          </ul>
        </section>
      ))}

      {seasonOver && <p className="state">Säsongen är slut. Inga fler händelser är inlagda.</p>}

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
              ? 'Dölj tidigare händelser'
              : `Visa ${String(pastCount)} tidigare ${pastCount === 1 ? 'händelse' : 'händelser'}`}
          </button>

          <div id={pastId} hidden={!showPast}>
            {past.map((group) => (
              <section key={group.key} aria-labelledby={`tidigare-${group.key}`}>
                <h3 id={`tidigare-${group.key}`} className="match-list__heading">
                  {group.heading}
                </h3>
                <ul className="match-list__items">
                  {group.events.map((event) => (
                    <EventCard key={event.id} event={event} />
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
