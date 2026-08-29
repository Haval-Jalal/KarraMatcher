import { ApiError } from '@/lib/api'

import { MatchList } from './MatchList'
import { useTeamMatches } from './useTeamMatches'

/**
 * Containern runt matchlistan. Hämtar schemat och hanterar varje tillstånd.
 *
 * Fyra utfall kräver olika text, eftersom de kräver olika saker av läsaren: nätet är
 * nere, laget finns inte, servern strular, eller allt fungerar. "Något gick fel" hade
 * varit enklare att skriva och sämre att läsa.
 */
export function MatchListSection({ slug }: { slug: string }) {
  const { data, isPending, error, refetch, isFetching } = useTeamMatches(slug)

  if (isPending) {
    return (
      <p className="state" role="status">
        Hämtar matcherna…
      </p>
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

  return <MatchList matches={data.matches} />
}
