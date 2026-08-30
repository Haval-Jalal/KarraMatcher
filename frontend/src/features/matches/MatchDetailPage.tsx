import { Link, useParams } from '@tanstack/react-router'

import { ApiError } from '@/lib/api'
import { formatKickoffTime, formatMatchDate, relativeDayLabel } from '@/lib/time'

import { CalendarLink } from './CalendarLink'
import { DirectionsLink } from './DirectionsLink'
import { MatchWeather } from './MatchWeather'
import { useMatch } from './useMatch'

/**
 * En match på egen adress, t.ex. `/match/{id}`.
 *
 * Samlar allt en förälder behöver inför avfärd: när, var, mot vem, och om matchen alls
 * blir av. Adressen är delbar och nås direkt från en kalenderpost — Vercels SPA-fallback
 * gör att en djuplänk fungerar även utan att någon varit på startsidan först (§KM.11).
 *
 */
export function MatchDetailPage() {
  const { id } = useParams({ from: '/match/$id' })
  const { data, isPending, error, refetch, isFetching } = useMatch(id)

  if (isPending) {
    return (
      <main>
        <p className="state" role="status">
          Hämtar matchen…
        </p>
      </main>
    )
  }

  if (error) {
    const apiError = error instanceof ApiError ? error : null

    return (
      <main>
        <div className="state state--error" role="alert">
          <p>{errorMessage(apiError)}</p>
          {apiError?.status !== 404 && (
            <button
              type="button"
              className="button"
              onClick={() => {
                void refetch()
              }}
              disabled={isFetching}
            >
              {isFetching ? 'Försöker…' : 'Försök igen'}
            </button>
          )}
        </div>
        <p>
          <Link to="/">Till startsidan</Link>
        </p>
      </main>
    )
  }

  const { match, team } = data
  const isCancelled = match.status === 'Cancelled'
  const isPostponed = match.status === 'Postponed'

  return (
    <main style={{ '--team-accent': team.colorHex } as React.CSSProperties}>
      <header className="app-header">
        <p className="app-header__subtitle">
          <Link to="/lag/$slug" params={{ slug: team.slug }}>
            ← {team.ageGroup} {team.name}
          </Link>
        </p>
        <h1>
          {match.isHome ? 'Hemma mot ' : 'Borta mot '}
          {match.opponent}
        </h1>
      </header>

      {/*
        Statusen står först och som text, inte som en färgad ram runt sidan: den ändrar
        allt annat på sidan och måste nå fram även till den som inte skiljer färger
        (WCAG 1.4.1). role="status" gör att en skärmläsare läser den utan att användaren
        behöver leta.
      */}
      {isCancelled && (
        <p className="notice notice--cancelled" role="status">
          <strong>Matchen är inställd.</strong> Åk inte till spelplatsen.
        </p>
      )}

      {isPostponed && (
        <p className="notice" role="status">
          <strong>Matchen är framflyttad.</strong> Nytt datum är inte satt än — tiden nedan är den
          som gällde tidigare.
        </p>
      )}

      <dl className="detail">
        <div className="detail__row">
          <dt>När</dt>
          <dd>
            {formatMatchDate(match.kickoffUtc)} kl. {formatKickoffTime(match.kickoffUtc)}
            <span className="detail__hint"> ({relativeDayLabel(match.kickoffUtc)})</span>
          </dd>
        </div>

        <div className="detail__row">
          <dt>Var</dt>
          <dd>
            {match.venue.name}
            <br />
            <span className="detail__hint">{match.address}</span>
          </dd>
        </div>

        <div className="detail__row">
          <dt>Match</dt>
          <dd>{match.isHome ? 'Hemmamatch' : 'Bortamatch'}</dd>
        </div>

        {/*
          Vädret renderar sig självt till ingenting när matchen ligger för långt fram
          eller anropet misslyckats, så raden försvinner helt i stället för att stå tom.
        */}
        <MatchWeather match={match} />
      </dl>

      {/*
        Åtgärderna gäller en match som ska spelas som planerat. Är den inställd eller
        framflyttad utan nytt datum skulle en vägbeskrivning leda någon till en plan där
        ingen match äger rum — det är precis den irrelevanta åtgärd #21 talar om.
      */}
      {!isCancelled && !isPostponed && (
        <div className="actions">
          <DirectionsLink venueName={match.venue.name} address={match.address} />
          <CalendarLink match={match} />
        </div>
      )}
    </main>
  )
}

function errorMessage(error: ApiError | null): string {
  if (error?.status === 404) {
    return 'Matchen finns inte. Länken kan vara gammal, eller så har matchen tagits bort.'
  }

  if (error?.offline) {
    return 'Ingen anslutning. Matchen kan inte hämtas just nu.'
  }

  return 'Kunde inte hämta matchen just nu.'
}
