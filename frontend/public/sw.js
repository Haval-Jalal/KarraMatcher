/*
 * Service worker för Kärra Matcher (§KM.8).
 *
 * Täckningen vid fotbollsplanerna är opålitlig, så lagets schema och appskalet ska gå att
 * läsa utan nät. Appen är däremot **offline-medveten, inte offline-först**: skrivningar
 * köas inte, och användaren får tydligt besked i stället.
 *
 * Handskriven och utan bibliotek, med bara runtime-cache. Det som normalt kräver ett
 * verktyg är att förcacha listan över byggda filer med deras innehållshashar — och det
 * behövs inte här, eftersom filerna cachas när de hämtas första gången.
 *
 * ── Den viktigaste regeln ──────────────────────────────────────────────────────────
 *
 * Ett svar med `no-store` eller `private` i `Cache-Control` sparas aldrig. Backend sätter
 * exakt de headrarna på allt som inte uttryckligen är publikt, vilket betyder att samma
 * header som håller ett svar borta från Vercels edge håller det borta härifrån. Auth-svar
 * kan därmed inte hamna i cachen ens om någon glömmer en särskild regel för dem.
 */

// Byt version för att slänga gamla cachar. Namnen är prefixade så att städningen nedan
// bara rör våra egna.
const VERSION = 'v1'
const SHELL_CACHE = `karra-skal-${VERSION}`
const DATA_CACHE = `karra-data-${VERSION}`
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
            .filter((name) => name !== SHELL_CACHE && name !== DATA_CACHE)
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

  // Kalenderfiler laddas ner och hör hemma i telefonens kalender, inte i vår cache.
  if (url.pathname.startsWith('/calendar/')) {
    return
  }

  // Byggda filer är innehållshashade: samma namn betyder alltid samma innehåll.
  if (url.pathname.startsWith('/assets/')) {
    event.respondWith(cacheFirst(request, SHELL_CACHE))
    return
  }

  if (url.pathname.startsWith('/api/')) {
    event.respondWith(networkFirst(request, DATA_CACHE))
    return
  }

  // Ikoner, manifest och annat i roten.
  event.respondWith(networkFirst(request, SHELL_CACHE))
})
