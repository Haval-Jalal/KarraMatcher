import { Link, useRouterState } from '@tanstack/react-router'

import { useAuth } from '@/features/auth'

/**
 * Appens huvudnavigering — en fast botten-navbar (ersätter den gamla hamburgaren).
 *
 * <h3>Där tummen når</h3>
 *
 * De mest använda vyerna ligger synliga i en rad längst ned; resten bor bakom "Mer". Det är
 * mönstret icke-tekniska föräldrar känner igen från andra appar, och det tar bort dagens
 * vänster-knapp-som-öppnar-en-panel-från-höger.
 *
 * <h3>Stabil per roll</h3>
 *
 * Navbaren ser likadan ut för alla inloggade — tränar-, admin- och superadmin-vyerna når man
 * via "Mer", inte via extra flikar. En meny som byter innehåll när man klickar runt är svår att
 * lita på (`#253`).
 *
 * <h3>Menyn visar, den tillåter inget</h3>
 *
 * Flikarna är bekvämlighet, ingen säkerhetsgräns: varje route prövar medlemskap och roll
 * server-side oavsett vad navbaren visar (§KM.3).
 */
export function BottomNav() {
  const { status } = useAuth()
  const pathname = useRouterState({ select: (state) => state.location.pathname })

  const current = tabOf(pathname)
  const loggedIn = status === 'inloggad'

  return (
    <nav className="bottom-nav" aria-label="Huvudmeny">
      <ul className="bottom-nav__list">
        <BottomTab to="/" label="Hem" icon="🏠" active={current === 'schema'} />

        {loggedIn && <BottomTab to="/chatt" label="Chatt" icon="💬" active={current === 'chatt'} />}

        {loggedIn && <BottomTab to="/cuper" label="Cuper" icon="🏆" active={current === 'cuper'} />}

        <BottomTab
          to="/spelarkort"
          label="Spelarkort"
          icon="⭐"
          active={current === 'spelarkort'}
        />

        <BottomTab to="/mer" label="Mer" icon="☰" active={current === 'mer'} />
      </ul>
    </nav>
  )
}

/** De statiska adresser navbaren länkar till — håller `to` typsäker mot routern. */
type NavTo = '/' | '/chatt' | '/cuper' | '/spelarkort' | '/mer'

function BottomTab({
  to,
  label,
  icon,
  active,
}: {
  to: NavTo
  label: string
  icon: string
  active: boolean
}) {
  return (
    <li className="bottom-nav__item">
      <Link className="bottom-nav__tab" to={to} aria-current={active ? 'page' : undefined}>
        <span className="bottom-nav__icon" aria-hidden="true">
          {icon}
        </span>
        <span className="bottom-nav__label">{label}</span>
      </Link>
    </li>
  )
}

type Tab = 'schema' | 'chatt' | 'cuper' | 'spelarkort' | 'mer' | null

/**
 * Vilken flik en adress hör till.
 *
 * <para>
 * Schemat är flera adresser (startsidan, ett lags schema, en enskild händelse) och alla ska
 * märka ut samma flik. Vyerna som bor under "Mer" (konto, tränar-/admin-/superadmin, integritet
 * och inloggning) märker ut Mer-fliken. En adress utan flik ger `null` — en 404-sida ska inte
 * påstå att man står i schemat.
 * </para>
 */
function tabOf(pathname: string): Tab {
  if (pathname.startsWith('/spelarkort')) return 'spelarkort'
  if (pathname.startsWith('/chatt') || /^\/lag\/[^/]+\/chatt/.test(pathname)) return 'chatt'
  if (pathname.startsWith('/cuper')) return 'cuper'

  if (
    pathname === '/mer' ||
    pathname === '/logga-in' ||
    pathname === '/konto' ||
    pathname === '/admin' ||
    pathname === '/superadmin' ||
    pathname.startsWith('/integritet') ||
    /^\/lag\/[^/]+\/tranare/.test(pathname)
  ) {
    return 'mer'
  }

  if (pathname === '/' || pathname.startsWith('/lag/') || pathname.startsWith('/handelse/')) {
    return 'schema'
  }

  return null
}
