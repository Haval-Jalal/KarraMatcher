import { useTeams } from './useTeams'
import { useSelectedTeam } from './useSelectedTeam'

/**
 * Accentfärgen för det valda laget, eller null innan något är valt.
 *
 * Härleds under render i stället för att lagras vid sidan av valet. Två källor till samma
 * sanning hade kunnat glida isär — och färgen hade då blivit kvar på fel lag efter en
 * omladdning, vilket är precis det lagväljaren finns för att förhindra.
 */
export function useTeamAccent(): string | null {
  const { data: teams } = useTeams()
  const { selectedSlug } = useSelectedTeam()

  if (!teams || selectedSlug === null) {
    return null
  }

  return teams.find((team) => team.slug === selectedSlug)?.colorHex ?? null
}
