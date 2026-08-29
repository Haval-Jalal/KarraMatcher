import { TeamPicker } from './TeamPicker'
import { useSelectedTeam } from './useSelectedTeam'
import { useTeams } from './useTeams'
import { ApiError } from '@/lib/api'

/**
 * Containern runt lagväljaren: hämtar lagen och hanterar varje tillstånd.
 *
 * Appen används på fotbollsplaner med dålig täckning, så offline är ett normalläge och
 * inte ett undantag (CLAUDE.md → Frontend). Alla fem tillstånd hanteras uttryckligen:
 * laddar, offline, fel, tomt och data.
 */
export function TeamPickerSection() {
  const { data: teams, isPending, error, refetch, isFetching } = useTeams()
  const { selectedSlug, selectTeam } = useSelectedTeam()

  if (isPending) {
    return (
      <p className="state" role="status">
        Hämtar lagen…
      </p>
    )
  }

  if (error) {
    // Ett nätverksfel och ett serverfel kräver olika saker av användaren: det ena går
    // över av sig självt, det andra gör det inte. Att kalla båda "något gick fel" hade
    // varit enklare och sämre.
    const offline = error instanceof ApiError && error.offline

    return (
      <div className="state state--error" role="alert">
        <p>
          {offline
            ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
            : 'Kunde inte hämta lagen just nu.'}
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

  if (teams.length === 0) {
    return (
      <p className="state" role="status">
        Inga lag är upplagda än.
      </p>
    )
  }

  return <TeamPicker teams={teams} selectedSlug={selectedSlug} onSelect={selectTeam} />
}
