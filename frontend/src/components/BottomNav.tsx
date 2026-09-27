import { Link, useRouterState } from '@tanstack/react-router'
import type { ComponentType, SVGProps } from 'react'

import { useAuth } from '@/features/auth'

import { ActivityIcon, ChatIcon, HomeIcon, MenuIcon, StarIcon, TrophyIcon } from './NavIcons'

/**
 * Appens huvudnavigering — en fast botten-navbar (ersätter den gamla hamburgaren).
 *
 * <h3>Där tummen når</h3>
 *
 * De mest använda vyerna ligger synliga i en rad längst ned; resten bor bakom "Mer". Det är
 * mönstret icke-tekniska föräldrar känner igen från andra appar.
 *
 * <h3>Egna ikoner + glidande markör (`#redesign`)</h3>
 *
 * Ikonerna är egna linje-ikoner (inte emoji, som hänger dåligt ihop och ser daterat ut), och en
 * accent-markör glider mellan flikarna när man byter sida. Markören placeras utifrån den aktiva
 * flikens plats bland de <em>synliga</em> flikarna, så den sitter rätt även för en gäst som ser
 * färre flikar.
 *
 * <h3>Menyn visar, den tillåter inget</h3>
 *
 * Flikarna är bekvämlighet, ingen säkerhetsgräns: varje route prövar medlemskap och roll
 * server-side oavsett vad navbaren visar (§KM.3).
 */

type Tab = 'schema' | 'aktivitet' | 'chatt' | 'cuper' | 'spelarkort' | 'mer'

/** De statiska adresser navbaren länkar till — håller `to` typsäker mot routern. */
type NavTo = '/' | '/aktivitet' | '/chatt' | '/cuper' | '/spelarkort' | '/mer'

interface NavTab {
  to: NavTo
  label: string
  tab: Tab
  Icon: ComponentType<SVGProps<SVGSVGElement>>
  /** Flikar som även en gäst ser (resten kräver inloggning). */
  guest: boolean
}

const TABS: NavTab[] = [
  { to: '/', label: 'Hem', tab: 'schema', Icon: HomeIcon, guest: true },
  { to: '/aktivitet', label: 'Aktivitet', tab: 'aktivitet', Icon: ActivityIcon, guest: false },
  { to: '/chatt', label: 'Chatt', tab: 'chatt', Icon: ChatIcon, guest: false },
  { to: '/cuper', label: 'Cuper', tab: 'cuper', Icon: TrophyIcon, guest: false },
  { to: '/spelarkort', label: 'Spelarkort', tab: 'spelarkort', Icon: StarIcon, guest: true },
  { to: '/mer', label: 'Mer', tab: 'mer', Icon: MenuIcon, guest: true },
]

export function BottomNav() {
  const { status } = useAuth()
  const pathname = useRouterState({ select: (state) => state.location.pathname })

  const loggedIn = status === 'inloggad'
  const visible = TABS.filter((tab) => tab.guest || loggedIn)

  const current = tabOf(pathname)
  const activeIndex = visible.findIndex((tab) => tab.tab === current)

  return (
    <nav className="bottom-nav" aria-label="Huvudmeny">
      <ul className="bottom-nav__list" style={{ ['--nav-count' as string]: visible.length }}>
        {/*
          Den glidande markören. Ligger utanför flikarna så den kan animeras fritt; göms när
          ingen flik är aktiv (t.ex. en 404-sida) så den inte pekar ut fel plats.
        */}
        <span
          className="bottom-nav__blob"
          aria-hidden="true"
          data-hidden={activeIndex === -1 ? 'true' : undefined}
          style={{ ['--nav-index' as string]: Math.max(0, activeIndex) }}
        />

        {visible.map(({ to, label, tab, Icon }) => (
          <li key={to} className="bottom-nav__item">
            <Link
              className="bottom-nav__tab"
              to={to}
              aria-current={tab === current ? 'page' : undefined}
            >
              <Icon className="bottom-nav__icon" />
              <span className="bottom-nav__label">{label}</span>
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  )
}

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
function tabOf(pathname: string): Tab | null {
  if (pathname.startsWith('/spelarkort')) return 'spelarkort'
  if (pathname.startsWith('/aktivitet')) return 'aktivitet'
  if (pathname.startsWith('/chatt') || /^\/lag\/[^/]+\/chatt/.test(pathname)) return 'chatt'
  if (pathname.startsWith('/cuper')) return 'cuper'

  if (
    pathname === '/mer' ||
    pathname === '/logga-in' ||
    pathname === '/konto' ||
    pathname === '/installningar' ||
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
