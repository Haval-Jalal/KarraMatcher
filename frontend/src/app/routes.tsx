import { createRootRoute, createRoute, createRouter, redirect } from '@tanstack/react-router'

import { NotFound } from '@/components/NotFound'
import { RootLayout } from '@/app/RootLayout'
import { CoachEventsPage } from '@/features/admin'
import { AccountPage, LoginPage } from '@/features/auth'
import { ChatPage, TeamChatPage } from '@/features/chat'
import { ChildrenPage, PlayerCardPage } from '@/features/playercard'
import { PrivacyPage } from '@/features/privacy'
import { EventDetailPage, TeamSchedulePage } from '@/features/events'
import { ApplyLandingPage } from '@/features/applications'
import { AdminPage, InvitationLandingPage } from '@/features/invitations'
import { StartPage } from '@/features/start/StartPage'
import { SuperAdminPage } from '@/features/superadmin'
import { SELECTED_TEAM_STORAGE_KEY } from '@/features/teams/selectedTeamContext'
import { renewSession } from '@/lib/api'
import { getAccessToken } from '@/lib/session'
import { readSetting } from '@/lib/storage'

const rootRoute = createRootRoute({
  component: RootLayout,
  notFoundComponent: NotFound,
})

/**
 * Kräver en inloggad session för en route, annars omdirigering till inloggningen (§KM.3).
 *
 * <h3>Varför i `beforeLoad` och inte i komponenten</h3>
 *
 * En utloggad ska aldrig se en skyddad vy blinka förbi — och framför allt ska hen inte
 * utlösa det API-anrop som den stängda backend ändå svarar `401` på. Det anropet kan inte
 * cachas på edgen och skulle väcka Render i onödan (§KM.11). Grinden fångar det före anropet.
 *
 * <h3>Sessionen förlängs först</h3>
 *
 * Access-token lever bara en kvart. En återvändande förälder med en giltig refresh-cookie
 * ska slippa inloggningsrutan, så vi försöker förnya innan vi ger upp. Bara den som verkligen
 * saknar session skickas vidare — med `next`, så att hen kommer tillbaka dit hen var på väg.
 *
 * <para>
 * Grinden är bekvämlighet, inte skydd: servern avgör vad som faktiskt får läsas (§KM.3).
 * Den här sparar bara en gäst från ett 401 där en inloggningsuppmaning hade varit svaret.
 * </para>
 */
async function requireSession(pathname: string): Promise<void> {
  // Saknas access-token i minnet försöker vi förnya mot refresh-cookien (§KM.11, `#255`) —
  // alltid, utan localStorage-ledtråd, så en installerad app loggar in sig själv från cookien
  // även efter att iOS gallrat lagringen. Lyckas det inte skickas gästen till inloggningen.
  if (getAccessToken() === null) {
    await renewSession()
  }

  if (getAccessToken() === null) {
    // TanStack Router signalerar omdirigering genom att man kastar resultatet av redirect().
    // Det är ramverkets dokumenterade API och inte ett kastat undantag.
    // eslint-disable-next-line @typescript-eslint/only-throw-error
    throw redirect({ to: '/logga-in', search: { next: pathname } })
  }
}

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
  beforeLoad: async ({ location }) => {
    // Startsidan listar lagen den inloggade hör till (§KM.3) — en gäst har inget att se här.
    await requireSession(location.pathname)

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
 * Lagets schema. Kräver inloggning (§KM.3) — en delad adress landar en medlem direkt på rätt
 * lag, men en gäst möts av inloggningen i stället för ett tomt schema. Medlemskapet i laget
 * avgör servern; grinden här sparar bara gästen från ett 401.
 */
const teamRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/lag/$slug',
  beforeLoad: ({ location }) => requireSession(location.pathname),
  component: TeamSchedulePage,
})

/** Exporteras för tester, som bygger en egen router med minneshistorik. */
/**
 * En händelse på egen adress. Nås från listan, från "nästa"-kortet, och från en push-notis.
 * Kräver inloggning (§KM.3) — händelsen är truppens interna, inte en publik matchtid längre;
 * medlemskapet avgör servern, grinden sparar gästen från ett 401.
 */
const eventRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/handelse/$id',
  beforeLoad: ({ location }) => requireSession(location.pathname),
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
  beforeLoad: ({ location }) => requireSession(location.pathname),
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
  beforeLoad: ({ location }) => requireSession(location.pathname),
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
  beforeLoad: ({ location }) => requireSession(location.pathname),
  component: SuperAdminPage,
})

/**
 * Trupp-adminens vy (§KM.3, `#193`). Kräver inloggning; att man är admin för en trupp avgör
 * servern och vyn (den göms för andra).
 */
const adminRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/admin',
  beforeLoad: ({ location }) => requireSession(location.pathname),
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
 * Trupp-chatten (§KM.1/§KM.10, `#201`). Kräver inloggning i `beforeLoad`; medlemskapet i
 * truppen prövas server-side. `/chatt` landar på medlemmens första trupp, `/chatt/{truppId}`
 * är push-notisens djuplänk och förväljer den truppen.
 */
const chatIndexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/chatt',
  beforeLoad: ({ location }) => requireSession(location.pathname),
  component: ChatPage,
})

const chatTruppRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/chatt/$truppId',
  beforeLoad: ({ location }) => requireSession(location.pathname),
  component: ChatPage,
})

/**
 * Lag-chatten (§KM.1/§KM.10, `#202`) — en egen kanal per färg-lag, nådd från lagsidan. Kräver
 * inloggning i `beforeLoad`; medlemskapet i laget prövas server-side.
 */
const teamChatRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/lag/$slug/chatt',
  beforeLoad: ({ location }) => requireSession(location.pathname),
  component: TeamChatPage,
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
  chatIndexRoute,
  chatTruppRoute,
  teamChatRoute,
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
