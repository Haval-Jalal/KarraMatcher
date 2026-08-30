import {
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  redirect,
} from '@tanstack/react-router'

import { NotFound } from '@/components/NotFound'
import { MatchDetailPage, TeamSchedulePage } from '@/features/matches'
import { StartPage } from '@/features/start/StartPage'
import { SELECTED_TEAM_STORAGE_KEY } from '@/features/teams/selectedTeamContext'
import { readSetting } from '@/lib/storage'

const rootRoute = createRootRoute({
  component: () => <Outlet />,
  notFoundComponent: NotFound,
})

/**
 * Startsidan skickar vidare till senast valda lag.
 *
 * Omdirigeringen sker i `beforeLoad` och inte i en effekt, så att en återvändande förälder
 * aldrig ser lagväljaren blinka förbi på väg till sitt schema. Finns inget sparat val
 * visas väljaren — det är förstagångsbesökarens vy.
 */
const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  beforeLoad: () => {
    const remembered = readSetting(SELECTED_TEAM_STORAGE_KEY)

    if (remembered !== null && remembered !== '') {
      // TanStack Router signalerar omdirigering genom att man kastar resultatet av
      // redirect(). Det är ramverkets dokumenterade API och inte ett kastat undantag,
      // så only-throw-error gäller inte här.
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ to: '/lag/$slug', params: { slug: remembered } })
    }
  },
  component: StartPage,
})

/**
 * Lagets schema. Adressen är delbar — en förälder skickar den i föräldragruppen och
 * mottagaren landar på rätt lag utan att välja något.
 */
const teamRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/lag/$slug',
  component: TeamSchedulePage,
})

/** Exporteras för tester, som bygger en egen router med minneshistorik. */
/**
 * En match på egen adress. Nås från listan, från "nästa match"-kortet, och så småningom
 * direkt från en kalenderpost eller en push-notis.
 */
const matchRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/match/$id',
  component: MatchDetailPage,
})

export const routeTree = rootRoute.addChildren([indexRoute, teamRoute, matchRoute])

export const router = createRouter({
  routeTree,
  defaultPreload: 'intent',
})

// Gör routerns typer kända för hela appen, så länkar och parametrar blir typsäkra.
declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}
