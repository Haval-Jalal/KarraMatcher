import { TeamPickerSection, useSelectedTeam, useTeamAccent, useTeams } from '@/features/teams'

/**
 * Startsidan. I dag lagväljaren; matchlistan kommer i #19.
 *
 * Accentfärgen sätts som CSS-variabel på sidans yttersta element i stället för på varje
 * komponent. Då kan headern, korten och knapparna läsa samma värde utan att någon av dem
 * behöver veta något om lag.
 */
export function StartPage() {
  const accent = useTeamAccent()
  const { selectedSlug } = useSelectedTeam()
  const { data: teams } = useTeams()

  const selectedTeam = teams?.find((team) => team.slug === selectedSlug)

  return (
    <main style={accent ? ({ '--team-accent': accent } as React.CSSProperties) : undefined}>
      <header className="app-header">
        <h1>Kärra Matcher</h1>
        <p className="app-header__subtitle">
          {selectedTeam
            ? `${selectedTeam.ageGroup} ${selectedTeam.name}`
            : 'Välj lag för att se matcherna'}
        </p>
      </header>

      <h2 id="valj-lag">Lag</h2>
      <TeamPickerSection />
    </main>
  )
}
