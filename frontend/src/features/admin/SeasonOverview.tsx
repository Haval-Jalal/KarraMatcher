import { eventLabel, type TeamEvent } from '@/features/events'
import { formatKickoffTime, formatMatchDate, formatMonthHeading } from '@/lib/time'

import { findClashes } from './findClashes'

/**
 * Hela säsongen på en skärm.
 *
 * <h3>Varför spelade händelser är kvar</h3>
 *
 * Tränaren behöver överblick, inte bara nästa händelse. Det är i helheten hen upptäcker att
 * två krockar, att en omgång saknas, eller att något hamnat i fel månad.
 *
 * <h3>Krockar</h3>
 *
 * Markeringen är ett <em>ord</em>, inte en färg (WCAG 1.4.1), och står i samma rad som
 * händelsen.
 */
export function SeasonOverview({
  events,
  onEdit,
  onCancel,
  onDelete,
}: {
  events: readonly TeamEvent[]
  onEdit: (event: TeamEvent) => void
  onCancel: (event: TeamEvent) => void
  onDelete: (event: TeamEvent) => void
}) {
  if (events.length === 0) {
    return (
      <p className="state">
        Inga händelser inlagda än. Klistra in schemat ovan, eller lägg till en i taget.
      </p>
    )
  }

  const clashing = findClashes(events)
  const byMonth = groupByMonth(events)

  return (
    <>
      {clashing.size > 0 && (
        <p className="state state--error" role="status">
          {`${String(clashing.size)} händelser ligger närmare än två timmar från varandra. `}
          Kontrollera att det stämmer.
        </p>
      )}

      {byMonth.map(([month, group]) => (
        <section key={month} className="season">
          <h3 className="season__month">{month}</h3>

          <div className="scroll">
            <table className="season__table">
              <thead>
                <tr>
                  <th scope="col">När</th>
                  <th scope="col">Vad</th>
                  <th scope="col">Plats</th>
                  <th scope="col">Åtgärd</th>
                </tr>
              </thead>
              <tbody>
                {group.map((event) => (
                  <tr key={event.id}>
                    <td>
                      <span className="season__time">{formatKickoffTime(event.kickoffUtc)}</span>{' '}
                      <span className="season__date">{formatMatchDate(event.kickoffUtc)}</span>
                      {clashing.has(event.id) && (
                        <span className="badge badge--cancelled"> Krock</span>
                      )}
                      {event.status === 'Cancelled' && <span className="badge"> Inställd</span>}
                    </td>
                    <td>{eventLabel(event)}</td>
                    <td className="season__venue">{event.venue.name}</td>
                    <td>
                      <div className="season__actions">
                        <button
                          type="button"
                          className="button"
                          onClick={() => {
                            onEdit(event)
                          }}
                        >
                          <span aria-hidden="true">Ändra</span>
                          <span className="visually-hidden">{`Ändra ${eventLabel(event)}`}</span>
                        </button>

                        {event.status !== 'Cancelled' && (
                          <button
                            type="button"
                            className="button"
                            onClick={() => {
                              onCancel(event)
                            }}
                          >
                            <span aria-hidden="true">Ställ in</span>
                            <span className="visually-hidden">{`Ställ in ${eventLabel(event)}`}</span>
                          </button>
                        )}

                        <button
                          type="button"
                          className="button button--danger"
                          onClick={() => {
                            onDelete(event)
                          }}
                        >
                          <span aria-hidden="true">Ta bort</span>
                          <span className="visually-hidden">{`Ta bort ${eventLabel(event)}`}</span>
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      ))}
    </>
  )
}

/**
 * Händelserna per månad, i tidsordning.
 */
function groupByMonth(events: readonly TeamEvent[]): [string, TeamEvent[]][] {
  const groups = new Map<string, TeamEvent[]>()

  for (const event of [...events].sort((a, b) => a.kickoffUtc.localeCompare(b.kickoffUtc))) {
    const month = formatMonthHeading(event.kickoffUtc)
    const existing = groups.get(month)

    if (existing === undefined) {
      groups.set(month, [event])
    } else {
      existing.push(event)
    }
  }

  return [...groups.entries()]
}
