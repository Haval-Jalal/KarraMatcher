import { LoadingState } from '@/components/LoadingState'
import { ApiError } from '@/lib/api'

import { EventList } from './EventList'
import { ScheduleSkeleton } from './ScheduleSkeleton'
import { NextEventCard } from './NextEventCard'
import { selectNextEvent } from './selectNextEvent'
import { useTeamEvents } from './useTeamEvents'

/**
 * Containern runt schemat: nästa händelse-kortet och händelselistan. Hämtar en gång och
 * äger ordningen dem emellan, så att kortet alltid hamnar överst.
 *
 * Fyra utfall kräver olika text: nätet är nere, laget finns inte, servern strular, eller
 * allt fungerar. "Något gick fel" hade varit enklare att skriva och sämre att läsa.
 */
export function EventListSection({ slug }: { slug: string }) {
  const { data, isPending, error, refetch, isFetching } = useTeamEvents(slug)

  if (isPending) {
    return (
      <LoadingState label="Hämtar schemat…">
        <ScheduleSkeleton />
      </LoadingState>
    )
  }

  if (error) {
    const apiError = error instanceof ApiError ? error : null

    if (apiError?.status === 404) {
      return (
        <p className="state state--error" role="alert">
          Laget finns inte. Kontrollera länken — laget kan ha bytt namn.
        </p>
      )
    }

    return (
      <div className="state state--error" role="alert">
        <p>
          {apiError?.offline
            ? 'Ingen anslutning. Schemat kan inte hämtas just nu.'
            : 'Kunde inte hämta schemat just nu.'}
        </p>
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
      </div>
    )
  }

  // Väljs en gång och matas till båda. Kortet och listan kan därmed aldrig bli oense om
  // vilken händelse som är nästa.
  const next = selectNextEvent(data.events)

  return (
    <>
      {next && <NextEventCard event={next} />}
      <h2 className="match-list__title">Schema</h2>
      <EventList events={data.events} {...(next ? { excludeId: next.id } : {})} />
    </>
  )
}
