import { Link } from '@tanstack/react-router'

import { LoadingState } from '@/components/LoadingState'
import { eventTypeLabel } from '@/features/events'
import { TeamPicker, useTeams } from '@/features/teams'
import { ApiError } from '@/lib/api'
import { formatKickoffTime, formatMatchDate } from '@/lib/time'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import type { HomeEvent, HomePendingKallelse } from './homeApi'
import { useHomeSummary } from './useHomeSummary'

/**
 * Hem — landningsvyn ("allt samlat").
 *
 * <h3>En översikt, inte en lagväljare</h3>
 *
 * Startsidan var förr bara en väljare som skickade vidare till ett lags schema. Här ser man i
 * stället direkt vad som är på gång: nästa händelse, kallelser som väntar på svar, och det
 * senaste i chatten — allt tvärs över ens lag, hämtat i ett anrop. Lagen finns kvar längst ned
 * som väg in i hela schemat.
 *
 * <h3>Bara det man ändå får se</h3>
 *
 * Sammanställningen bär ingen ny data: servern scopar den till kontots egna medlemskap, och
 * fritext (chatt) visas bara för den som redan är med i kanalen (§KM.1/§KM.3).
 */
export function HomePage() {
  useDocumentTitle('Hem')

  const summary = useHomeSummary()
  const teams = useTeams()

  const nextEvent = summary.data?.nextEvent ?? null
  const pending = summary.data?.pendingKallelser ?? []
  const latestChat = summary.data?.latestChat ?? null

  return (
    <main>
      <header className="app-header">
        <h1>Hem</h1>
        <p className="app-header__subtitle">Allt som är på gång för dina lag.</p>
      </header>

      {summary.isPending && <LoadingState label="Hämtar din översikt…" />}

      {summary.isError && (
        <div className="state state--error" role="alert">
          <p>
            {summary.error instanceof ApiError && summary.error.offline
              ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
              : 'Kunde inte hämta översikten just nu.'}
          </p>
          <button
            type="button"
            className="button"
            onClick={() => {
              void summary.refetch()
            }}
            disabled={summary.isFetching}
          >
            {summary.isFetching ? 'Försöker…' : 'Försök igen'}
          </button>
        </div>
      )}

      {summary.data && (
        <>
          <section aria-labelledby="hem-nasta">
            <h2 id="hem-nasta">Nästa händelse</h2>
            {nextEvent ? (
              <Link
                to="/handelse/$id"
                params={{ id: nextEvent.id }}
                className="hem-card hem-card--link"
              >
                <p className="hem-card__when">
                  <span className="hem-card__time">{formatKickoffTime(nextEvent.kickoffUtc)}</span>
                  <span className="hem-card__date">{formatMatchDate(nextEvent.kickoffUtc)}</span>
                </p>
                <p className="hem-card__title">{headline(nextEvent)}</p>
                <p className="hem-card__meta">
                  {nextEvent.teamName}
                  {nextEvent.place ? ` · ${nextEvent.place}` : ''}
                </p>
              </Link>
            ) : (
              <p className="state">Inga kommande händelser just nu.</p>
            )}
          </section>

          {pending.length > 0 && (
            <section aria-labelledby="hem-kallelser">
              <h2 id="hem-kallelser">Väntar på ditt svar</h2>
              <ul className="hem-list">
                {pending.map((item) => (
                  <li key={item.eventId}>
                    <Link
                      to="/handelse/$id"
                      params={{ id: item.eventId }}
                      className="hem-card hem-card--link"
                    >
                      <p className="hem-card__when">
                        <span className="hem-card__time">{formatKickoffTime(item.kickoffUtc)}</span>
                        <span className="hem-card__date">{formatMatchDate(item.kickoffUtc)}</span>
                      </p>
                      <p className="hem-card__title">{headline(item)}</p>
                      <p className="hem-card__meta">
                        {item.teamName} · {unansweredLabel(item.unansweredCount)}
                      </p>
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          )}

          {latestChat !== null && (
            <section aria-labelledby="hem-chatt">
              <h2 id="hem-chatt">Senaste i chatten</h2>
              {latestChat.teamSlug !== null ? (
                <Link
                  to="/lag/$slug/chatt"
                  params={{ slug: latestChat.teamSlug }}
                  className="hem-card hem-card--link"
                >
                  <p className="hem-card__meta">
                    {latestChat.channelName} · {formatKickoffTime(latestChat.sentAtUtc)}
                  </p>
                  <p className="hem-card__title">{latestChat.snippet}</p>
                  <p className="hem-card__meta">{latestChat.authorName}</p>
                </Link>
              ) : (
                <Link
                  to="/chatt/$truppId"
                  params={{ truppId: latestChat.truppId }}
                  className="hem-card hem-card--link"
                >
                  <p className="hem-card__meta">
                    {latestChat.channelName} · {formatKickoffTime(latestChat.sentAtUtc)}
                  </p>
                  <p className="hem-card__title">{latestChat.snippet}</p>
                  <p className="hem-card__meta">{latestChat.authorName}</p>
                </Link>
              )}
            </section>
          )}
        </>
      )}

      <section aria-labelledby="hem-lag">
        <h2 id="hem-lag">Dina lag</h2>

        {teams.isPending && <LoadingState label="Hämtar lagen…" />}

        {teams.isError && (
          <p className="state state--error" role="alert">
            {teams.error instanceof ApiError && teams.error.offline
              ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
              : 'Kunde inte hämta lagen just nu.'}
          </p>
        )}

        {teams.data && teams.data.length === 0 && <p className="state">Inga lag är upplagda än.</p>}

        {teams.data && teams.data.length > 0 && (
          <TeamPicker teams={teams.data} currentSlug={null} />
        )}
      </section>
    </main>
  )
}

/** "Hemma mot X" för en match, annars rubriken — samma som schemat beskriver händelsen. */
function headline(item: HomeEvent | HomePendingKallelse): string {
  if (item.type === 'Match') {
    return `${item.isHome ? 'Hemma' : 'Borta'} mot ${item.opponent ?? ''}`.trim()
  }

  return item.title ?? eventTypeLabel(item.type)
}

function unansweredLabel(count: number): string {
  return count === 1 ? '1 barn har inte svarat' : `${count} barn har inte svarat`
}
