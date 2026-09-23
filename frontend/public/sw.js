/*
 * Service worker för Kärra Matcher (§KM.8).
 *
 * Täckningen vid fotbollsplanerna är opålitlig, så **appskalet och de statiska tillgångarna**
 * ska gå att läsa utan nät: den installerade appen öppnas och ger ett tydligt svenskt besked
 * i stället för webbläsarens felsida.
 *
 * **Innehållet kräver uppkoppling (v2, `#191`/`#249`).** Den stängda appen märker varje
 * API-svar `private, no-store`, och service workern cachar därför **ingenting under `/api/`** —
 * medlemmens schema, kallelser och samåkning hämtas färskt och lagras aldrig på enheten.
 * Offline når felet appen, som säger till. (I den öppna v1 cachades det publika schemat för
 * offline-läsning; det utgick när schemat blev medlemsstängt.) Appen är alltså
 * **offline-medveten, inte offline-först**: skrivningar köas inte, användaren får besked.
 *
 * Handskriven och utan bibliotek, med bara runtime-cache. Det som normalt kräver ett
 * verktyg är att förcacha listan över byggda filer med deras innehållshashar — och det
 * behövs inte här, eftersom filerna cachas när de hämtas första gången.
 *
 * ── Den viktigaste regeln ──────────────────────────────────────────────────────────
 *
 * `/api/` rör vi aldrig — anropet går rakt till nätet. Som extra skydd för de svar vi *ändå*
 * cachar (skalet, ikoner, manifest) gäller: ett svar med `no-store` eller `private` i
 * `Cache-Control` sparas aldrig. Samma header som håller ett svar borta från Vercels edge
 * håller det borta härifrån.
 */

// Byt version för att slänga gamla cachar. Namnen är prefixade så att städningen nedan
// bara rör våra egna. v3: `/api/` cachas inte längre; data-cachen är borttagen (`#249`).
const VERSION = 'v3'
const SHELL_CACHE = `karra-skal-${VERSION}`
const CACHE_PREFIX = 'karra-'

/** Appskalet. Allt annat cachas när det hämtas. */
const SHELL_URL = '/'

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(SHELL_CACHE).then((cache) => cache.add(SHELL_URL)))
  // Ingen skipWaiting här: den nya versionen ska vänta tills användaren säger till.
  // Att byta kod under fötterna på någon mitt i en sida är inte hjälpsamt.
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((names) =>
        Promise.all(
          names
            .filter((name) => name.startsWith(CACHE_PREFIX))
            .filter((name) => name !== SHELL_CACHE)
            .map((name) => caches.delete(name)),
        ),
      )
      .then(() => self.clients.claim()),
  )
})

/**
 * Sida som väntar ber oss ta över. Skickas först när användaren tryckt på "Ladda om".
 */
self.addEventListener('message', (event) => {
  if (event.data === 'SKIP_WAITING') {
    self.skipWaiting()
  }
})

/*
 * ── Notiser (§KM.10, `#244`) ──────────────────────────────────────────────────────
 *
 * Backend skickar en krypterad nyttolast med exakt `title`, `body` och `url` — aldrig något
 * om ett barn, ett spelarkort eller en förälders fritext (se PushMessage på serversidan). Vi
 * visar det som det är och sparar ingenting: en notis loggas inte och når inte vår cache.
 */
self.addEventListener('push', (event) => {
  // Utan data finns inget att visa. En tom notis är värre än ingen, så vi avstår hellre.
  if (!event.data) {
    return
  }

  let payload
  try {
    payload = event.data.json()
  } catch {
    return
  }

  const title =
    typeof payload.title === 'string' && payload.title !== '' ? payload.title : 'Kärra Matcher'
  const url = typeof payload.url === 'string' && payload.url.startsWith('/') ? payload.url : '/'

  event.waitUntil(
    self.registration.showNotification(title, {
      body: typeof payload.body === 'string' ? payload.body : '',
      icon: '/icon-192.png',
      badge: '/icon-192.png',
      // Samma mål samlas i en notis i stället för att stapla likadana: två samåkningssvar
      // för samma match blir en rad, inte två.
      tag: url,
      data: { url },
    }),
  )
})

/**
 * Ett klick öppnar appen på notisens adress — och återanvänder en redan öppen flik hellre
 * än att öppna en till, så att en förälder inte får tre Kärra Matcher-flikar av tre notiser.
 */
self.addEventListener('notificationclick', (event) => {
  event.notification.close()

  const data = event.notification.data
  const url = data && typeof data.url === 'string' ? data.url : '/'
  const target = new URL(url, self.location.origin).href

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      for (const client of windows) {
        if (client.url === target && 'focus' in client) {
          return client.focus()
        }
      }

      const open = windows.find((client) => 'focus' in client)

      if (open) {
        // En öppen flik navigeras dit och lyfts fram, om webbläsaren tillåter navigate().
        if ('navigate' in open) {
          return open.navigate(target).then((navigated) => (navigated ?? open).focus())
        }

        return open.focus()
      }

      return self.clients.openWindow ? self.clients.openWindow(target) : undefined
    }),
  )
})

/**
 * Sant om svaret får sparas.
 *
 * Ett svar utan `Cache-Control` behandlas som cachebart bara om det är ett vanligt 200 från
 * vår egen origin — allt annat är det inte värt risken att gissa om.
 */
function mayStore(response) {
  if (!response || !response.ok || response.status !== 200) {
    return false
  }

  const control = (response.headers.get('Cache-Control') || '').toLowerCase()

  return !control.includes('no-store') && !control.includes('private')
}

/** Nätet först, cachen som reserv. För data som ska vara färsk men helst finnas alls. */
async function networkFirst(request, cacheName) {
  try {
    const response = await fetch(request)

    if (mayStore(response)) {
      const cache = await caches.open(cacheName)
      await cache.put(request, response.clone())
    }

    return response
  } catch (error) {
    const cached = await caches.match(request)

    if (cached) {
      return cached
    }

    throw error
  }
}

/** Cachen först. Bara för innehållshashade filer, som aldrig ändras under samma namn. */
async function cacheFirst(request, cacheName) {
  const cached = await caches.match(request)

  if (cached) {
    return cached
  }

  const response = await fetch(request)

  if (mayStore(response)) {
    const cache = await caches.open(cacheName)
    await cache.put(request, response.clone())
  }

  return response
}

/** Navigering: nätet först, annars appskalet ur cachen. */
async function navigate(request) {
  try {
    return await fetch(request)
  } catch (error) {
    const shell = await caches.match(SHELL_URL)

    if (shell) {
      return shell
    }

    throw error
  }
}

self.addEventListener('fetch', (event) => {
  const request = event.request

  // Bara GET. En POST som cachas är ett fel som inte går att förklara för någon.
  if (request.method !== 'GET') {
    return
  }

  const url = new URL(request.url)

  // Andra origins lämnas i fred. Vädret från Open-Meteo hämtas direkt av sidan och ska
  // inte ligga kvar i vår cache — en gammal prognos är sämre än ingen.
  if (url.origin !== self.location.origin) {
    return
  }

  if (request.mode === 'navigate') {
    event.respondWith(navigate(request))
    return
  }

  // Byggda filer är innehållshashade: samma namn betyder alltid samma innehåll.
  if (url.pathname.startsWith('/assets/')) {
    event.respondWith(cacheFirst(request, SHELL_CACHE))
    return
  }

  // Autentiserat innehåll cachas aldrig (§KM.8, v2). Den stängda appen (#191) märker varje
  // API-svar `private, no-store`; att spara medlemmens schema på enheten vore att lagra privat
  // lagdata där den inte behövs. Vi låter anropet passera rakt till nätet — är nätet nere når
  // felet appen, som säger till på svenska i stället för att visa gammal data.
  if (url.pathname.startsWith('/api/')) {
    return
  }

  // Ikoner, manifest och annat i roten.
  event.respondWith(networkFirst(request, SHELL_CACHE))
})
