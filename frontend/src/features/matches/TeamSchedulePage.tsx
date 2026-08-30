import { useParams } from '@tanstack/react-router'
import { useEffect } from 'react'

import { TeamPicker, useSelectedTeam, useTeams } from '@/features/teams'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { MatchListSection } from './MatchListSection'

/**
 * Ett lags schema på egen adress, t.ex. `/lag/gul`.
 *
 * Adressen är kontraktet: en förälder som skickar länken i föräldragruppen ska veta att
 * mottagaren landar på rätt lag. Därför styr URL:en vilket lag som visas, medan det
 * sparade valet bara avgör vart en besökare skickas från startsidan.
 */
export function TeamSchedulePage() {
  const { slug } = useParams({ from: '/lag/$slug' })
  const { data: teams } = useTeams()
  const { selectedSlug, selectTeam } = useSelectedTeam()

  // Att öppna en delad länk ska också bli det ihågkomna valet — annars skickas
  // föräldern tillbaka till sitt gamla lag nästa gång hen öppnar appen.
  useEffect(() => {
    if (slug !== selectedSlug) {
      selectTeam(slug)
    }
  }, [slug, selectedSlug, selectTeam])

  const team = teams?.find((candidate) => candidate.slug === slug)
  const accent = team?.colorHex

  useDocumentTitle(team ? `${team.ageGroup} ${team.name}` : 'Matcher')

  return (
    <main style={accent ? ({ '--team-accent': accent } as React.CSSProperties) : undefined}>
      <header className="app-header">
        <h1>Kärra Matcher</h1>
        <p className="app-header__subtitle">
          {team ? `${team.ageGroup} ${team.name}` : 'Laget hämtas…'}
        </p>
      </header>

      {teams && teams.length > 0 && (
        <>
          <h2>Lag</h2>
          <TeamPicker teams={teams} currentSlug={slug} />
        </>
      )}

      <MatchListSection slug={slug} />
    </main>
  )
}
