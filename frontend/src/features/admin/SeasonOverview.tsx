import type { Match } from '@/features/matches'
import { formatKickoffTime, formatMatchDate, formatMonthHeading } from '@/lib/time'

import { findClashes } from './findClashes'

/**
 * Hela säsongen på en skärm.
 *
 * <h3>Varför spelade matcher är kvar</h3>
 *
 * Tränaren behöver överblick, inte bara nästa match. Det är i helheten hen upptäcker att
 * två matcher krockar, att en omgång saknas, eller att en match hamnat i fel månad — och
 * en lista som börjar vid dagens datum döljer just det.
 *
 * <h3>Krockar</h3>
 *
 * Markeringen är ett <em>ord</em>, inte en färg (WCAG 1.4.1), och står i samma rad som
 * matchen. En varning i en egen ruta högst upp kräver att man själv letar reda på vilken
 * rad den gäller.
 */
export function SeasonOverview({
  matches,
  onEdit,
  onCancel,
  onDelete,
}: {
  matches: readonly Match[]
  onEdit: (match: Match) => void
  onCancel: (match: Match) => void
  onDelete: (match: Match) => void
}) {
  if (matches.length === 0) {
    return (
      <p className="state">
        Inga matcher inlagda än. Klistra in schemat ovan, eller lägg till en match i taget.
      </p>
    )
  }

  const clashing = findClashes(matches)
  const byMonth = groupByMonth(matches)

  return (
    <>
      {clashing.size > 0 && (
        <p className="state state--error" role="status">
          {`${String(clashing.size)} matcher ligger närmare än två timmar från varandra. `}
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
                  <th scope="col">Motståndare</th>
                  <th scope="col">Plats</th>
                  <th scope="col">Åtgärd</th>
                </tr>
              </thead>
              <tbody>
                {group.map((match) => (
                  <tr key={match.id}>
                    <td>
                      <span className="season__time">{formatKickoffTime(match.kickoffUtc)}</span>{' '}
                      <span className="season__date">{formatMatchDate(match.kickoffUtc)}</span>
                      {clashing.has(match.id) && (
                        <span className="badge badge--cancelled"> Krock</span>
                      )}
                      {match.status === 'Cancelled' && <span className="badge"> Inställd</span>}
                    </td>
                    <td>
                      {match.isHome ? 'Hemma mot ' : 'Borta mot '}
                      {match.opponent}
                    </td>
                    <td className="season__venue">{match.venue.name}</td>
                    <td>
                      <div className="season__actions">
                        <button
                          type="button"
                          className="button"
                          onClick={() => {
                            onEdit(match)
                          }}
                        >
                          <span aria-hidden="true">Ändra</span>
                          <span className="visually-hidden">
                            {`Ändra matchen mot ${match.opponent}`}
                          </span>
                        </button>

                        {match.status !== 'Cancelled' && (
                          <button
                            type="button"
                            className="button"
                            onClick={() => {
                              onCancel(match)
                            }}
                          >
                            <span aria-hidden="true">Ställ in</span>
                            <span className="visually-hidden">
                              {`Ställ in matchen mot ${match.opponent}`}
                            </span>
                          </button>
                        )}

                        <button
                          type="button"
                          className="button button--danger"
                          onClick={() => {
                            onDelete(match)
                          }}
                        >
                          <span aria-hidden="true">Ta bort</span>
                          <span className="visually-hidden">
                            {`Ta bort matchen mot ${match.opponent}`}
                          </span>
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
 * Matcherna per månad, i tidsordning.
 *
 * Månadsrubriker och inte en enda lång tabell: en säsong är ett trettiotal rader, och
 * rubrikerna är det som gör den möjlig att hoppa i på en telefon.
 */
function groupByMonth(matches: readonly Match[]): [string, Match[]][] {
  const groups = new Map<string, Match[]>()

  for (const match of [...matches].sort((a, b) => a.kickoffUtc.localeCompare(b.kickoffUtc))) {
    const month = formatMonthHeading(match.kickoffUtc)
    const existing = groups.get(month)

    if (existing === undefined) {
      groups.set(month, [match])
    } else {
      existing.push(match)
    }
  }

  return [...groups.entries()]
}
