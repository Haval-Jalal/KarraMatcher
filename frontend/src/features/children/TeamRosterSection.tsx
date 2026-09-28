import { ApiError } from '@/lib/api'

import { useTeamRoster } from './useChildren'

/**
 * Lagtränarens läsvy över sitt lag (`#redesign`, §KM.3): barnen i laget och deras
 * vårdnadshavares kontaktuppgifter (namn + mejl). Enbart läsning — att ändra barn eller koppla
 * vårdnadshavare är adminens sak. Servern (policyn `CoachOfTeam`) avgör åtkomsten; en tränare
 * ser bara sitt eget lag.
 *
 * <h3>Barn-PII hålls minimal</h3>
 *
 * Ett barn visas alltid som "Liam J" — aldrig hela efternamnet (§KM.1). Mejladressen är en
 * vuxens egen uppgift och visas för lagets tränare, precis som för admin.
 */
export function TeamRosterSection({ slug }: { slug: string }) {
  const roster = useTeamRoster(slug)

  return (
    <section className="team-roster" aria-labelledby="team-roster-rubrik">
      <h2 id="team-roster-rubrik" className="match-list__title">
        Laget
      </h2>

      {roster.isPending && (
        <p className="state" role="status">
          Hämtar laget…
        </p>
      )}

      {roster.isError && (
        <p className="state state--error" role="alert">
          {roster.error instanceof ApiError && roster.error.offline
            ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
            : 'Kunde inte hämta laget just nu.'}
        </p>
      )}

      {roster.data && roster.data.children.length === 0 && (
        <p className="state">Inga barn i laget än. Klubbens admin lägger till dem.</p>
      )}

      {roster.data && roster.data.children.length > 0 && (
        <ul className="roster-list">
          {roster.data.children.map((child) => (
            <li key={child.id} className="roster-list__item">
              <p className="roster-list__name">{child.displayName}</p>
              {child.guardians.length === 0 ? (
                <p className="admin-muted">Ingen vårdnadshavare kopplad än.</p>
              ) : (
                <ul className="roster-list__guardians">
                  {child.guardians.map((guardian) => (
                    <li key={guardian.accountId}>
                      <span className="roster-list__guardian-name">
                        {guardian.displayName ?? guardian.email}
                      </span>
                      <a className="roster-list__guardian-mail" href={`mailto:${guardian.email}`}>
                        {guardian.email}
                      </a>
                    </li>
                  ))}
                </ul>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
