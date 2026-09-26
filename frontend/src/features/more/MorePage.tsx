import { Link, useNavigate } from '@tanstack/react-router'

import { useAuth } from '@/features/auth'

/**
 * "Mer"-sidan — botten-navbarens overflow.
 *
 * <h3>De vanligaste vyerna bor i navbaren, resten här</h3>
 *
 * Botten-navbaren bär det man når varje vecka (schema, chatt, cuper, spelarkort). Konto,
 * roll-vyerna (tränare/admin/superadmin), integritetstexten och utloggningen ligger ett steg
 * bort — här. Navbaren hålls därmed stabil för alla, oavsett roll (samma lärdom som `#253`).
 *
 * <h3>En egen route, inte en utfällning</h3>
 *
 * `/mer` är en riktig sida. Då fungerar bakåtknappen, djuplänkning och skärmläsare precis som
 * för vilken vy som helst — till skillnad från en meny som fälls ut och försvinner.
 *
 * <h3>Menyn visar, den tillåter inget</h3>
 *
 * Roll-länkarna syns bara för rätt roll, men det är bekvämlighet och ingen gräns —
 * auktoriseringen ligger i backend (§KM.3).
 */
export function MorePage() {
  const { status, coachOf, adminOf, isSuperAdmin, signOut } = useAuth()
  const navigate = useNavigate()

  const loggedIn = status === 'inloggad'

  // Lag-tränarlänken pekar på det första lag man är tränare för. Rollen styr ensam om den syns
  // — aldrig sidan man råkar stå på, annars bytte menyn innehåll när man klickade runt (`#253`).
  const coachTeam = coachOf[0] ?? null

  async function handleSignOut(): Promise<void> {
    await signOut()
    await navigate({ to: '/logga-in' })
  }

  return (
    <main>
      <header className="app-header">
        <h1>Mer</h1>
      </header>

      <nav aria-label="Mer i menyn">
        <ul className="mer-list">
          {loggedIn && coachTeam !== null && (
            <li>
              <Link className="mer-link" to="/lag/$slug/tranare" params={{ slug: coachTeam }}>
                Sköt laget
              </Link>
            </li>
          )}

          {loggedIn && adminOf.length > 0 && (
            <li>
              <Link className="mer-link" to="/admin">
                Tränare
              </Link>
            </li>
          )}

          {loggedIn && isSuperAdmin && (
            <li>
              <Link className="mer-link" to="/superadmin">
                Superadmin
              </Link>
            </li>
          )}

          {loggedIn && (
            <li>
              <Link className="mer-link" to="/konto">
                Mitt konto
              </Link>
            </li>
          )}

          <li>
            <Link className="mer-link" to="/integritet">
              Så hanteras dina uppgifter
            </Link>
          </li>

          {/*
            Ingenting av inloggning/konto medan status är 'okänd'. Sessionen förnyas vid start,
            och att visa "Logga in" under den halvsekunden hade blinkat till för varje inloggad
            förälder (samma skäl som den gamla menyn, `#255`).
          */}
          {status === 'utloggad' && (
            <li>
              <Link className="mer-link" to="/logga-in">
                Logga in
              </Link>
            </li>
          )}

          {loggedIn && (
            <li>
              <button
                type="button"
                className="mer-link mer-link--action"
                onClick={() => void handleSignOut()}
              >
                Logga ut
              </button>
            </li>
          )}
        </ul>
      </nav>
    </main>
  )
}
