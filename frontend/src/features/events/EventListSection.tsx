import { useState } from 'react'

import { LoadingState } from '@/components/LoadingState'
import { ApiError } from '@/lib/api'

import { EventList } from './EventList'
import { ScheduleSkeleton } from './ScheduleSkeleton'
import { NextEventCard } from './NextEventCard'
import { selectNextEvent } from './selectNextEvent'
import type { TeamEvent } from './types'
import { useTeamEvents } from './useTeamEvents'

/** Typfiltret på schemat (`#304`): "Alla" plus en flik per händelsetyp. */
const FILTERS: { key: 'Alla' | TeamEvent['type']; label: string }[] = [
  { key: 'Alla', label: 'Alla' },
  { key: 'Match', label: 'Matcher' },
  { key: 'Training', label: 'Träningar' },
  { key: 'Cup', label: 'Cuper' },
  { key: 'Other', label: 'Övrigt' },
]

/**
 * Containern runt schemat: nästa händelse-kortet och händelselistan. Hämtar en gång och
 * äger ordningen dem emellan, så att kortet alltid hamnar överst.
 *
 * Fyra utfall kräver olika text: nätet är nere, laget finns inte, servern strular, eller
 * allt fungerar. "Något gick fel" hade varit enklare att skriva och sämre att läsa.
 */
export function EventListSection({ slug }: { slug: string }) {
  const { data, isPending, error, refetch, isFetching } = useTeamEvents(slug)
  const [filter, setFilter] = useState<'Alla' | TeamEvent['type']>('Alla')

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

  const hasAny = data.events.length > 0
  const events = filter === 'Alla' ? data.events : data.events.filter((e) => e.type === filter)

  // Väljs en gång och matas till båda. Kortet och listan kan därmed aldrig bli oense om
  // vilken händelse som är nästa. Med ett filter aktivt är "nästa" nästa av den typen.
  const next = selectNextEvent(events)

  return (
    <>
      {next && <NextEventCard event={next} />}

      <h2 className="match-list__title">Schema</h2>

      {/* Filtret visas bara när det finns något att filtrera; annars äger EventList tomläget. */}
      {hasAny && (
        <div className="schedule-filter" role="group" aria-label="Filtrera schemat">
          {FILTERS.map((option) => (
            <button
              key={option.key}
              type="button"
              className={
                filter === option.key
                  ? 'schedule-filter__tab schedule-filter__tab--active'
                  : 'schedule-filter__tab'
              }
              aria-pressed={filter === option.key}
              onClick={() => setFilter(option.key)}
            >
              {option.label}
            </button>
          ))}
        </div>
      )}

      {hasAny && events.length === 0 ? (
        <p className="state">Inget att visa för det här valet.</p>
      ) : (
        <EventList events={events} {...(next ? { excludeId: next.id } : {})} />
      )}
    </>
  )
}
