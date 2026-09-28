import { Link } from '@tanstack/react-router'

import { LoadingState } from '@/components/LoadingState'
import { useAuth } from '@/features/auth'
import { eventTypeLabel } from '@/features/events'
import { TeamPicker, useTeams } from '@/features/teams'
import { ApiError } from '@/lib/api'
import { formatKickoffTime, formatMatchDate, relativeDayLabel } from '@/lib/time'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import type { HomeEvent, HomePendingKallelse } from './homeApi'
import { useHomeSummary } from './useHomeSummary'

/**
 * Hem — landningsvyn ("allt samlat").
 *
 * <h3>En översikt, inte en lagväljare</h3>
 *
 * Startsidan var förr bara en väljare som skickade vidare till ett lags schema. Här ser man i
 * stället direkt vad som är på gång: nästa händelse och kallelser som väntar på svar — allt
 * tvärs över ens lag, hämtat i ett anrop. Lagen finns kvar längst ned som väg in i schemat, och
 * en admin får en ingång till hela truppen (`#redesign`).
 *
 * <h3>Bara det man ändå får se</h3>
 *
 * Sammanställningen bär ingen ny data: servern scopar den till kontots egna medlemskap. Truppen-
 * ingången är rollstyrd i UI:t men servern (AdminOfTrupp) är den riktiga grinden (§KM.3).
 */
export function HomePage() {
  useDocumentTitle('Hem')

  const summary = useHomeSummary()
  const teams = useTeams()
  const { adminOf } = useAuth()

  const nextEvent = summary.data?.nextEvent ?? null
  const pending = summary.data?.pendingKallelser ?? []

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
          <section className="hem-section" aria-labelledby="hem-nasta">
            <h2 id="hem-nasta" className="hem-section__label">
              Nästa händelse
            </h2>
            {nextEvent ? (
              <Link to="/handelse/$id" params={{ id: nextEvent.id }} className="hem-hero">
                <span className="hem-hero__eyebrow">
                  {relativeDayLabel(nextEvent.kickoffUtc)} ·{' '}
                  {formatKickoffTime(nextEvent.kickoffUtc)}
                </span>
                <span className="hem-hero__title">{headline(nextEvent)}</span>
                <span className="hem-hero__meta">
                  {formatMatchDate(nextEvent.kickoffUtc)}
                  {nextEvent.place ? ` · ${nextEvent.place}` : ''}
                </span>
                <span className="hem-hero__team">{nextEvent.teamName}</span>
              </Link>
            ) : (
              <p className="state">Inga kommande händelser just nu.</p>
            )}
          </section>

          {/*
            Statistik-rutorna från designriktningen (`#redesign`). Bara riktiga siffror: antal
            obesvarade kallelser (0 = allt besvarat) och antal lag man är med i. Artefaktens
            "Nya i chatten"-ruta utelämnas medvetet — API:t har ingen olästa-räknare (§KM.2).
          */}
          <div className="hem-tiles">
            <div className="hem-tile">
              <span className="hem-tile__n">{pending.length}</span>
              <span className="hem-tile__l">Obesvarade kallelser</span>
            </div>
            <div className="hem-tile">
              <span className="hem-tile__n">{teams.data?.length ?? '—'}</span>
              <span className="hem-tile__l">Dina lag</span>
            </div>
          </div>

          {pending.length > 0 && (
            <section className="hem-section" aria-labelledby="hem-kallelser">
              <h2 id="hem-kallelser" className="hem-section__label">
                Väntar på ditt svar
              </h2>
              <ul className="hem-list">
                {pending.map((item) => (
                  <li key={item.eventId}>
                    <Link
                      to="/handelse/$id"
                      params={{ id: item.eventId }}
                      className="hem-card hem-card--link hem-card--pending"
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
        </>
      )}

      {/*
        Truppen — adminens ingång till hela truppen (barn, färg-lag, vårdnadshavare). Rollstyrd:
        syns bara för den som är admin för minst en trupp. Synlighet, inte säkerhet — servern
        (AdminOfTrupp) är grinden (§KM.3). Länkar till den första truppen; TruppPage har en
        väljare för den som är admin i flera.
      */}
      {adminOf.length > 0 && (
        <section className="hem-section" aria-labelledby="hem-trupp">
          <h2 id="hem-trupp" className="hem-section__label">
            Truppen
          </h2>
          <Link
            to="/trupp/$truppId"
            params={{ truppId: adminOf[0]! }}
            className="hem-card hem-card--link"
          >
            <p className="hem-card__title">Hela truppen</p>
            <p className="hem-card__meta">Barn, färg-lag och vårdnadshavare</p>
          </Link>
        </section>
      )}

      <section className="hem-section" aria-labelledby="hem-lag">
        <h2 id="hem-lag" className="hem-section__label">
          Dina lag
        </h2>

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
