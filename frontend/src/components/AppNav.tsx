import { Link, useRouterState } from '@tanstack/react-router'

import { useAuth } from '@/features/auth'

/**
 * Appens huvudmeny (`#148`).
 *
 * <h3>Varför den behövdes i efterhand</h3>
 *
 * Fyra milstolpar byggde varsin sida och antog att någon annan kopplade ihop dem. Resultatet
 * var att spelarkortet, inloggningen och kontosidan låg i drift utan att gå att nå annat än
 * genom att skriva adressen — appen såg ut att bestå av enbart matchschemat.
 *
 * <h3>Menyn visar, den tillåter inget</h3>
 *
 * Tränarlänken syns bara för den som är tränare, men det är ett bekvämlighetsval och ingen
 * säkerhetsgräns. Den som ändrar sin egen token får se länken och möts av `403` på andra
 * sidan — auktoriseringen ligger i backend, precis som `coachTeamsFromToken` redan påpekar.
 */
export function AppNav() {
  const { status, coachOf, isAdmin } = useAuth()
  const pathname = useRouterState({ select: (state) => state.location.pathname })

  const teamInPath = /^\/lag\/([^/]+)/.exec(pathname)?.[1] ?? null

  /*
   * Vilket lag tranarlanken pekar pa. Star man redan pa ett lag man skoter ar det laget
   * ratt svar; annars det forsta man ar tranare for. En administrator som inte tittar pa
   * nagot lag far ingen lank -- vi vet inte vilket lag hen menade, och att gissa at en
   * administrator ar samre an att lata hen valja lag forst.
   */
  const coachTeam =
    teamInPath !== null && (isAdmin || coachOf.includes(teamInPath))
      ? teamInPath
      : (coachOf[0] ?? null)

  const current = sectionOf(pathname)

  return (
    <nav className="app-nav" aria-label="Huvudmeny">
      <ul className="app-nav__list">
        <li>
          <Link
            className="app-nav__link"
            to="/"
            aria-current={current === 'matcher' ? 'page' : undefined}
          >
            Matcher
          </Link>
        </li>

        <li>
          <Link
            className="app-nav__link"
            to="/spelarkort"
            aria-current={current === 'spelarkort' ? 'page' : undefined}
          >
            Spelarkort
          </Link>
        </li>

        {status === 'inloggad' && coachTeam !== null && (
          <li>
            <Link
              className="app-nav__link"
              to="/lag/$slug/tranare"
              params={{ slug: coachTeam }}
              aria-current={current === 'tranare' ? 'page' : undefined}
            >
              Tränare
            </Link>
          </li>
        )}

        {/*
          Ingenting alls medan status ar 'okand'. Sessionen fornyas vid start, och att visa
          "Logga in" under den halvsekunden hade blinkat till for varje inloggad foralder --
          det ser ut som att appen glomt bort en.
        */}
        {status === 'utloggad' && (
          <li>
            <Link
              className="app-nav__link"
              to="/logga-in"
              aria-current={current === 'logga-in' ? 'page' : undefined}
            >
              Logga in
            </Link>
          </li>
        )}

        {status === 'inloggad' && (
          <li>
            <Link
              className="app-nav__link"
              to="/konto"
              aria-current={current === 'konto' ? 'page' : undefined}
            >
              Mitt konto
            </Link>
          </li>
        )}
      </ul>
    </nav>
  )
}

type Section = 'matcher' | 'spelarkort' | 'tranare' | 'logga-in' | 'konto' | null

/**
 * Vilken meny­post adressen hör till.
 *
 * <para>
 * Matchdelen är tre adresser — startsidan, lagets schema och en enskild match — och alla
 * tre ska märka ut samma post. En adress som inte hör till någon post ger `null` i stället
 * för att falla tillbaka på den första: en 404-sida ska inte påstå att man står i schemat.
 * </para>
 */
function sectionOf(pathname: string): Section {
  if (pathname.endsWith('/tranare')) return 'tranare'
  if (pathname.startsWith('/spelarkort')) return 'spelarkort'
  if (pathname === '/logga-in') return 'logga-in'
  if (pathname === '/konto') return 'konto'

  if (pathname === '/' || pathname.startsWith('/lag/') || pathname.startsWith('/match/')) {
    return 'matcher'
  }

  return null
}
