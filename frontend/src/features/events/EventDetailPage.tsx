import { Link, useParams } from '@tanstack/react-router'

import { LoadingState } from '@/components/LoadingState'
import { ApiError } from '@/lib/api'
import { teamThemeStyle } from '@/lib/teamTheme'
import { formatKickoffTime, formatMatchDate, relativeDayLabel } from '@/lib/time'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { AttendanceSection } from '@/features/attendance'
import { CarpoolSection } from '@/features/carpool'
import { CupSignupSection } from '@/features/cup'
import { MatchReportCard, readCard } from '@/features/playercard'

import { DirectionsLink } from './DirectionsLink'
import { EventWeather } from './EventWeather'
import { eventLabel, eventTypeLabel } from './types'
import { useEvent } from './useEvent'

/**
 * En händelse på egen adress, t.ex. `/handelse/{id}`.
 *
 * Samlar allt en förälder behöver inför avfärd: när, var, vad, och om den alls blir av.
 * Adressen är delbar och nås direkt från en push-notis — Vercels SPA-fallback gör att en
 * djuplänk fungerar även utan att någon varit på startsidan först (§KM.11).
 *
 * Samåkning och spelarkort gäller matcher; kallelsen gäller match och träning (§KM.7).
 * Övrig händelse visar ingen av dem.
 */
export function EventDetailPage() {
  const { id } = useParams({ from: '/handelse/$id' })
  const { data, isPending, error, refetch, isFetching } = useEvent(id)

  useDocumentTitle(data ? eventLabel(data.event) : 'Händelse')

  if (isPending) {
    return (
      <main>
        <LoadingState label="Hämtar händelsen…" />
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

  const { event, team, truppId } = data
  const isMatch = event.type === 'Match'
  // Kallelsen gäller match och träning (§KM.7): hela truppen tränar ihop, och en match
  // kallar ett lag som fylls på med barn ur andra lag. Övrig händelse har ingen kallelse.
  const hasKallelse = event.type === 'Match' || event.type === 'Training'

  /*
   * Barnen som spelar i det har laget. Lases direkt fran enheten, inte genom en hook.
   */
  const childrenForMatch = readCard().children.filter(
    (child) => child.teamSlug === null || child.teamSlug === team.slug,
  )
  const isCancelled = event.status === 'Cancelled'
  const isPostponed = event.status === 'Postponed'

  return (
    <main style={teamThemeStyle(team.colorHex)}>
      <header className="app-header">
        <p className="app-header__subtitle">
          <Link to="/lag/$slug" params={{ slug: team.slug }}>
            ← {team.ageGroup} {team.name}
          </Link>
        </p>
        <h1>{eventLabel(event)}</h1>
      </header>

      {/*
        Statusen står först och som text, inte som en färgad ram: den ändrar allt annat och
        måste nå fram även till den som inte skiljer färger (WCAG 1.4.1). role="status" gör
        att en skärmläsare läser den utan att användaren behöver leta.
      */}
      {isCancelled && (
        <p className="notice notice--cancelled" role="status">
          <strong>Händelsen är inställd.</strong> Åk inte till spelplatsen.
        </p>
      )}

      {isPostponed && (
        <p className="notice" role="status">
          <strong>Händelsen är framflyttad.</strong> Nytt datum är inte satt än — tiden nedan är den
          som gällde tidigare.
        </p>
      )}

      {/*
        Avsparkstiden är hjälten: det man öppnar sidan för. Stor och tunn (Barlow Condensed
        300) med datum och relativ dag som en lugn eyebrow ovanför. Inställd match stryker
        över tiden — en form, inte bara en färg (WCAG 1.4.1).
      */}
      <div className="event-hero">
        <p className="event-hero__kicker">
          {formatMatchDate(event.kickoffUtc)} · {relativeDayLabel(event.kickoffUtc)}
        </p>
        <p
          className={
            isCancelled ? 'event-hero__time event-hero__time--cancelled' : 'event-hero__time'
          }
        >
          {formatKickoffTime(event.kickoffUtc)}
        </p>
      </div>

      <dl className="detail">
        <div className="detail__row">
          <dt>Var</dt>
          <dd>
            {event.venue.name !== '' && (
              <>
                {event.venue.name}
                <br />
              </>
            )}
            <span className="detail__hint">{event.address}</span>
          </dd>
        </div>

        <div className="detail__row">
          <dt>Typ</dt>
          <dd>
            {isMatch ? (event.isHome ? 'Hemmamatch' : 'Bortamatch') : eventTypeLabel(event.type)}
          </dd>
        </div>

        {/*
          Vädret renderar sig självt till ingenting när händelsen ligger för långt fram
          eller anropet misslyckats, så raden försvinner helt i stället för att stå tom.
        */}
        <EventWeather event={event} />
      </dl>

      {/*
        Åtgärderna gäller en händelse som ska äga rum som planerat. Är den inställd eller
        framflyttad utan nytt datum skulle en vägbeskrivning leda någon till en plan där
        inget händer.
      */}
      {!isCancelled && !isPostponed && (
        <div className="actions">
          <DirectionsLink venueName={event.venue.name} address={event.address} />
        </div>
      )}

      {/*
        Samåkning och spelarkort gäller matcher (§KM.12/§KM.2); kallelsen gäller match och
        träning (§KM.7). En övrig händelse visar ingen av dem.
      */}
      {isMatch && !isCancelled && <CarpoolSection match={event} />}

      {hasKallelse && !isCancelled && (
        <AttendanceSection
          eventId={event.id}
          truppId={truppId}
          teamName={team.name}
          kickoffUtc={event.kickoffUtc}
        />
      )}

      {/* Cupen har öppen anmälan i stället för kallelse (§KM.12, `#295`/`#296`). */}
      {event.type === 'Cup' && !isCancelled && (
        <CupSignupSection eventId={event.id} truppId={truppId} teamSlug={team.slug} />
      )}

      {isMatch && <MatchReportCard match={event} children={childrenForMatch} />}
    </main>
  )
}

function errorMessage(error: ApiError | null): string {
  if (error?.status === 404) {
    return 'Händelsen finns inte. Länken kan vara gammal, eller så har den tagits bort.'
  }

  if (error?.offline) {
    return 'Ingen anslutning. Händelsen kan inte hämtas just nu.'
  }

  return 'Kunde inte hämta händelsen just nu.'
}
