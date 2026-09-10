import { Link } from '@tanstack/react-router'

import { ApiError } from '@/lib/api'
import { formatKickoffTime, formatMatchDate } from '@/lib/time'

import type { TeamCarpoolMatch } from './carpoolApi'
import { seatCountLabel } from './carpoolLabels'
import { useTeamCarpool } from './useCarpool'

/**
 * Tränarens överblick över lagets samåkning (`#55`, §KM.12).
 *
 * <h3>Den viktigaste raden är den tomma</h3>
 *
 * Frågan tränaren har är "får alla skjuts till bortamatchen?". Därför står matcher
 * <em>utan</em> förare först i varje rads uppmärksamhet, med ord och inte bara med färg
 * (WCAG 1.4.1). En överblick som bara visade det som redan är ordnat hade varit trevlig
 * att titta på och omöjlig att agera på.
 *
 * <h3>Inga namn, för det finns inga</h3>
 *
 * Servern lagrar inget namn på en förälder — bara en mejladress, och den visas aldrig för
 * någon annan. Överblicken är därför räknad, inte namngiven. Det är inte en lucka som ska
 * fyllas; det är modellen (§KM.1).
 */
export function CarpoolOverview({ slug, enabled }: { slug: string; enabled: boolean }) {
  const { data, isPending, error, refetch, isFetching } = useTeamCarpool(slug, enabled)

  return (
    <section aria-labelledby="samakning-overblick">
      <h2 className="match-list__title" id="samakning-overblick">
        Samåkning
      </h2>

      {isPending && !error && (
        <p className="state" role="status">
          Hämtar samåkningen…
        </p>
      )}

      {error !== null && (
        <div className="state state--error" role="alert">
          <p>{errorMessage(error)}</p>
          <button
            type="button"
            className="button"
            disabled={isFetching}
            onClick={() => {
              void refetch()
            }}
          >
            {isFetching ? 'Försöker…' : 'Försök igen'}
          </button>
        </div>
      )}

      {data !== undefined && data.length === 0 && (
        <p className="carpool__empty">Inga kommande matcher att samåka till.</p>
      )}

      {data !== undefined && data.length > 0 && (
        <ul className="carpool-overview">
          {data.map((match) => (
            <li key={match.matchId} className="carpool-overview__row">
              <OverviewRow match={match} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function OverviewRow({ match }: { match: TeamCarpoolMatch }) {
  return (
    <>
      <p className="carpool-overview__match">
        <Link to="/match/$id" params={{ id: match.matchId }}>
          {match.isHome ? 'Hemma mot ' : 'Borta mot '}
          {match.opponent}
        </Link>
      </p>

      <p className="carpool-overview__when">
        {formatMatchDate(match.kickoffUtc)} kl. {formatKickoffTime(match.kickoffUtc)}
      </p>

      {match.needsDriver ? (
        <p className="carpool-overview__gap">Ingen har erbjudit skjuts än.</p>
      ) : (
        <p className="carpool-overview__seats">
          {seatCountLabel(match.seatsOffered)} erbjudna, {String(match.seatsTaken)} bokade,{' '}
          <strong>{seatsLeftLabel(match.seatsLeft)}</strong>
        </p>
      )}

      {/*
        Väntande förfrågningar är det andra tränaren kan göra något åt: en förare som inte
        hunnit svara är en familj som inte vet om den har skjuts.
      */}
      {match.pendingRequests > 0 && (
        <p className="carpool-overview__waiting">
          {match.pendingRequests === 1
            ? '1 förfrågan väntar på svar.'
            : `${String(match.pendingRequests)} förfrågningar väntar på svar.`}
        </p>
      )}
    </>
  )
}

function seatsLeftLabel(seatsLeft: number): string {
  if (seatsLeft === 0) {
    return 'inga platser kvar'
  }

  return seatsLeft === 1 ? '1 plats kvar' : `${String(seatsLeft)} platser kvar`
}

function errorMessage(error: unknown): string {
  return error instanceof ApiError && error.offline
    ? 'Ingen anslutning. Samåkningen kan inte hämtas just nu.'
    : 'Kunde inte hämta samåkningen just nu.'
}
