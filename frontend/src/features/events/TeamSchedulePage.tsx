import { Link, useParams } from '@tanstack/react-router'
import { useEffect } from 'react'

import { useAuth } from '@/features/auth'
import { TeamPicker, useSelectedTeam, useTeams } from '@/features/teams'
import { teamThemeStyle } from '@/lib/teamTheme'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { EventListSection } from './EventListSection'
import { useTeamEvents } from './useTeamEvents'

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
  const { status, canManage } = useAuth()

  // Schemats truppId (via den redan hämtade queryn — samma nyckel, inget extra anrop) låter en
  // trupp-tränare känna igen sitt eget lag och nå skötsel-vyn kontextuellt (§KM.7, `#287`).
  const { data: schedule } = useTeamEvents(slug)
  const mayManage = canManage(slug, schedule?.truppId)

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
    <main style={teamThemeStyle(accent)}>
      <header className="app-header">
        <h1>Truppen</h1>
        <p className="app-header__subtitle">
          {team ? (
            <span className="team-chip">
              {team.ageGroup} {team.name}
            </span>
          ) : (
            'Laget hämtas…'
          )}
        </p>
      </header>

      {/*
        Skötsel-länken är kontextuell: syns på det lag man tittar på, för den som får sköta det —
        per-lag-tränare, trupp-tränare (alla sina färg-lag) eller admin. Servern är grinden; det
        här styr bara vad som visas (`#287`).
      */}
      {mayManage && (
        <p className="actions">
          <Link className="button button--small" to="/lag/$slug/tranare" params={{ slug }}>
            Sköt laget
          </Link>
        </p>
      )}

      {teams && teams.length > 0 && (
        <>
          <h2>Lag</h2>
          <TeamPicker teams={teams} currentSlug={slug} />
        </>
      )}

      <EventListSection slug={slug} />

      {/* Lag-chatten (#202). Bara för inloggade; medlemskapet prövas server-side. */}
      {status === 'inloggad' && (
        <p className="actions">
          <Link className="button button--small" to="/lag/$slug/chatt" params={{ slug }}>
            Lagchatt
          </Link>
        </p>
      )}
    </main>
  )
}
