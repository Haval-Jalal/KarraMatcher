import { createRootRoute, createRoute, createRouter, redirect } from '@tanstack/react-router'

import { NotFound } from '@/components/NotFound'
import { RootLayout } from '@/app/RootLayout'
import { CoachEventsPage } from '@/features/admin'
import { AccountPage, LoginPage } from '@/features/auth'
import { ChildrenPage, PlayerCardPage } from '@/features/playercard'
import { PrivacyPage } from '@/features/privacy'
import { EventDetailPage, TeamSchedulePage } from '@/features/events'
import { ApplyLandingPage } from '@/features/applications'
import { AdminPage, InvitationLandingPage } from '@/features/invitations'
import { StartPage } from '@/features/start/StartPage'
import { SuperAdminPage } from '@/features/superadmin'
import { SELECTED_TEAM_STORAGE_KEY } from '@/features/teams/selectedTeamContext'
import { renewSession } from '@/lib/api'
import { getAccessToken, hasSessionHint } from '@/lib/session'
import { readSetting } from '@/lib/storage'

const rootRoute = createRootRoute({
  component: RootLayout,
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
 * En händelse på egen adress. Nås från listan, från "nästa"-kortet, och så småningom
 * direkt från en kalenderpost eller en push-notis.
 */
const eventRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/handelse/$id',
  component: EventDetailPage,
})

/**
 * Inloggning. `next` bär vart användaren var på väg, så hen kommer tillbaka dit.
 */
const loginRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/logga-in',
  validateSearch: (search: Record<string, unknown>): { next?: string } => {
    const next = search['next']

    /*
     * Bara adresser inom appen accepteras. Utan den kontrollen hade
     * `/logga-in?next=https://annan-sajt` skickat en nyss inloggad foralder vidare till
     * nagon annans sida -- en open redirect, och just efter en inloggning ar det den
     * dyraste sorten.
     */
    return typeof next === 'string' && next.startsWith('/') && !next.startsWith('//')
      ? { next }
      : {}
  },
  component: LoginPage,
})

/**
 * Kontosidan — appens första skyddade vy.
 *
 * Skyddet ligger i `beforeLoad` och inte i komponenten, så en utloggad aldrig ser sidan
 * blinka förbi. Sessionen förlängs först, eftersom access-token bara lever en kvart och
 * en återvändande förälder annars hade mötts av inloggningsrutan i onödan.
 */
const accountRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/konto',
  beforeLoad: async ({ location }) => {
    if (getAccessToken() === null && hasSessionHint()) {
      await renewSession()
    }

    if (getAccessToken() === null) {
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ to: '/logga-in', search: { next: location.pathname } })
    }
  },
  component: AccountPage,
})

/**
 * Tränarens vy för ett lag.
 *
 * Kräver inloggning här, och rätt lag i själva vyn. Servern avgör vad som faktiskt
 * tillåts — det här sparar bara en tränare från att mötas av ett 403 där en text hade
 * räckt.
 */
const coachRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/lag/$slug/tranare',
  beforeLoad: async ({ location }) => {
    if (getAccessToken() === null && hasSessionHint()) {
      await renewSession()
    }

    if (getAccessToken() === null) {
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ to: '/logga-in', search: { next: location.pathname } })
    }
  },
  component: CoachEventsPage,
})

/**
 * Superadmin-konsolen (§KM.3, `#192`).
 *
 * Kräver inloggning här; att man faktiskt är superadmin avgör servern (policyn
 * <c>SuperAdmin</c>) och vyn själv (den göms för andra). Route-grinden kontrollerar bara att
 * någon är inloggad — samma mönster som tränarvyn.
 */
const superadminRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/superadmin',
  beforeLoad: async ({ location }) => {
    if (getAccessToken() === null && hasSessionHint()) {
      await renewSession()
    }

    if (getAccessToken() === null) {
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ to: '/logga-in', search: { next: location.pathname } })
    }
  },
  component: SuperAdminPage,
})

/**
 * Trupp-adminens vy (§KM.3, `#193`). Kräver inloggning; att man är admin för en trupp avgör
 * servern och vyn (den göms för andra).
 */
const adminRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/admin',
  beforeLoad: async ({ location }) => {
    if (getAccessToken() === null && hasSessionHint()) {
      await renewSession()
    }

    if (getAccessToken() === null) {
      // eslint-disable-next-line @typescript-eslint/only-throw-error
      throw redirect({ to: '/logga-in', search: { next: location.pathname } })
    }
  },
  component: AdminPage,
})

/**
 * Inbjudningens landningssida (§KM.3, `#193`). Anonym — en inbjuden förälder ska kunna se
 * vart länken leder innan hen loggar in. Accepten kräver inloggning, det sköter sidan själv.
 */
const invitationRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/inbjudan/$token',
  component: InvitationLandingPage,
})

/**
 * Ansökningssidan (§KM.3, `#194`). Anonym — en förälder ska kunna se vilken trupp en delad
 * ansökningslänk leder till innan hen loggar in. Ansökan kräver inloggning, det sköter sidan.
 */
const applyRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/ansok/$truppId',
  component: ApplyLandingPage,
})

/**
 * Spelarkortet.
 *
 * Ingen inloggning: kortet ligger på enheten och kräver varken konto eller server
 * (§KM.2). Att skydda den här routen hade varit att kräva inloggning för att se sin egen
 * telefons innehåll.
 */
const playerCardRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/spelarkort',
  component: ChildrenPage,
})

/**
 * Barnets egen sida.
 *
 * <para>
 * Id:t i adressen är barnets lokala id och betyder ingenting utanför den här telefonen —
 * kortet ligger på enheten (§KM.2). En delad länk hit landar därför på en vänlig sida som
 * säger just det, inte på ett tomt kort.
 * </para>
 */
const playerCardChildRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/spelarkort/$childId',
  component: PlayerCardPage,
})

/**
 * Integritetstexten (§KM.6). Publik: en gäst ska kunna läsa vad appen sparar innan hen loggar
 * in, inte efter. Nås från fotens länk och från Mitt konto.
 */
const privacyRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/integritet',
  component: PrivacyPage,
})

export const routeTree = rootRoute.addChildren([
  indexRoute,
  teamRoute,
  eventRoute,
  loginRoute,
  accountRoute,
  coachRoute,
  superadminRoute,
  adminRoute,
  invitationRoute,
  applyRoute,
  playerCardRoute,
  playerCardChildRoute,
  privacyRoute,
])

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
