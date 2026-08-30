import { TeamPicker, useTeams } from '@/features/teams'
import { ApiError } from '@/lib/api'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

/**
 * Förstagångsbesökarens vy: välj lag.
 *
 * Har föräldern redan valt ett lag kommer hen aldrig hit — routern skickar vidare till
 * `/lag/<slug>` innan sidan renderas.
 */
export function StartPage() {
  const { data: teams, isPending, error, refetch, isFetching } = useTeams()

  useDocumentTitle('Välj lag')

  return (
    <main>
      <header className="app-header">
        <h1>Kärra Matcher</h1>
        <p className="app-header__subtitle">Välj lag för att se matcherna</p>
      </header>

      <h2>Lag</h2>

      {isPending && (
        <p className="state" role="status">
          Hämtar lagen…
        </p>
      )}

      {error && (
        <div className="state state--error" role="alert">
          <p>
            {error instanceof ApiError && error.offline
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
      )}

      {teams && teams.length === 0 && <p className="state">Inga lag är upplagda än.</p>}

      {teams && teams.length > 0 && <TeamPicker teams={teams} currentSlug={null} />}
    </main>
  )
}
