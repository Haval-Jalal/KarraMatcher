import { LoadingState } from '@/components/LoadingState'
import { ApiError } from '@/lib/api'

import { MatchList } from './MatchList'
import { ScheduleSkeleton } from './ScheduleSkeleton'
import { NextMatchCard } from './NextMatchCard'
import { TeamCalendarLink } from './TeamCalendarLink'
import { selectNextMatch } from './selectNextMatch'
import { useTeamMatches } from './useTeamMatches'

/**
 * Containern runt schemat: nästa match-kortet och matchlistan. Hämtar en gång och äger
 * ordningen dem emellan, så att kortet alltid hamnar överst.
 *
 * Fyra utfall kräver olika text, eftersom de kräver olika saker av läsaren: nätet är
 * nere, laget finns inte, servern strular, eller allt fungerar. "Något gick fel" hade
 * varit enklare att skriva och sämre att läsa.
 */
export function MatchListSection({ slug }: { slug: string }) {
  const { data, isPending, error, refetch, isFetching } = useTeamMatches(slug)

  if (isPending) {
    return (
      <LoadingState label="Hämtar matcherna…">
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
            : 'Kunde inte hämta matcherna just nu.'}
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
  // vilken match som är nästa — vilket är vad som gjorde att den visades två gånger.
  const next = selectNextMatch(data.matches)

  return (
    <>
      {next && <NextMatchCard match={next} />}
      <h2 className="match-list__title">Matcher</h2>
      <MatchList matches={data.matches} {...(next ? { excludeId: next.id } : {})} />
      <TeamCalendarLink slug={slug} />
    </>
  )
}
