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
  const { status, coachOf, isSuperAdmin, adminOf } = useAuth()
  const pathname = useRouterState({ select: (state) => state.location.pathname })

  const teamInPath = /^\/lag\/([^/]+)/.exec(pathname)?.[1] ?? null

  /*
   * Tranarlanken ar for den som ar tranare. Om man tittar pa ett lag man sjalv skoter
   * pekar lanken dit; annars pa det forsta laget man ar tranare for.
   *
   * Vilket lag den pekar pa far bero pa sidan -- men *om* den syns far det inte gora det.
   * Tidigare vidgade `isAdmin` villkoret sa att en administrator fick lanken bara pa en
   * lagsida (dar `teamInPath` fanns) och blev av med den overallt annars -- menyn bytte
   * innehall nar man klickade runt (`#253`). Nu styr rollen ensam: en tranare ser lanken
   * pa varje sida, en administrator som inte ar tranare ser den inte alls (hen skoter
   * truppen via Admin/Superadmin i stallet).
   */
  const coachTeam =
    teamInPath !== null && coachOf.includes(teamInPath) ? teamInPath : (coachOf[0] ?? null)

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

        {status === 'inloggad' && isSuperAdmin && (
          <li>
            <Link
              className="app-nav__link"
              to="/superadmin"
              aria-current={current === 'superadmin' ? 'page' : undefined}
            >
              Superadmin
            </Link>
          </li>
        )}

        {status === 'inloggad' && adminOf.length > 0 && (
          <li>
            <Link
              className="app-nav__link"
              to="/admin"
              aria-current={current === 'admin' ? 'page' : undefined}
            >
              Admin
            </Link>
          </li>
        )}

        {status === 'inloggad' && (
          <li>
            <Link
              className="app-nav__link"
              to="/chatt"
              aria-current={current === 'chatt' ? 'page' : undefined}
            >
              Chatt
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

type Section =
  | 'matcher'
  | 'spelarkort'
  | 'chatt'
  | 'tranare'
  | 'logga-in'
  | 'konto'
  | 'superadmin'
  | 'admin'
  | null

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
  if (pathname === '/superadmin') return 'superadmin'
  if (pathname === '/admin') return 'admin'
  if (pathname.startsWith('/chatt')) return 'chatt'

  if (pathname === '/' || pathname.startsWith('/lag/') || pathname.startsWith('/handelse/')) {
    return 'matcher'
  }

  return null
}
