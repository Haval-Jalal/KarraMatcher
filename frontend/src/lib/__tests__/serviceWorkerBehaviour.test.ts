import { beforeEach, describe, expect, it, vi } from 'vitest'

// `?raw` ger filens innehåll som en sträng. Alternativet vore node:fs, men det hade krävt
// Node-typer i samma tsconfig som webbläsarkoden — och därmed gjort det lätt att av
// misstag använda ett Node-API i en komponent.
import SW_SOURCE from '../../../public/sw.js?raw'

/**
 * Testerna kör **den fil som faktiskt levereras** — `public/sw.js` läses in och körs med
 * ett låtsat service worker-scope.
 *
 * Anledningen är att en kopia av logiken hade kunnat gå grön medan den riktiga filen var
 * trasig. En service worker är dessutom det enda i appen som inte går att återkalla från
 * servern: har den väl installerats hos någon ligger den kvar tills den ersätts.
 */

interface FakeCache {
  put: ReturnType<typeof vi.fn>
  add: ReturnType<typeof vi.fn>
}

interface Scope {
  fetchHandler: (event: FetchEvent) => void
  cache: FakeCache
  /** Vad service workerns `fetch` ska svara med härnäst. Sätts av varje test. */
  setNetwork: (impl: () => Promise<Response>) => void
}

interface FetchEvent {
  request: Request
  respondWith: (response: Promise<Response> | Response) => void
}

/** Startar service workern i ett låtsat scope och ger tillbaka dess lyssnare. */
function startWorker(options: { cachedResponse?: Response } = {}): Scope {
  const listeners: Record<string, (event: unknown) => void> = {}

  const cache: FakeCache = { put: vi.fn(), add: vi.fn().mockResolvedValue(undefined) }

  const caches = {
    open: vi.fn().mockResolvedValue(cache),
    match: vi.fn().mockResolvedValue(options.cachedResponse),
    keys: vi.fn().mockResolvedValue([]),
    delete: vi.fn().mockResolvedValue(true),
  }

  const self = {
    addEventListener: (type: string, handler: (event: unknown) => void) => {
      listeners[type] = handler
    },
    location: { origin: 'https://karra-matcher.vercel.app' },
    clients: { claim: vi.fn() },
    skipWaiting: vi.fn(),
    registration: {},
  }

  /*
   * Nätet ges till service workern som en omdirigering till en variabel testet styr.
   *
   * Att skicka in `globalThis.fetch` direkt var första försöket, och det gjorde att
   * testerna gick ut på riktiga internet: workern fångade den äkta funktionen innan
   * stubben hann sättas. Svaren kom från den driftsatta sajten, och assertions om vad som
   * cachades mätte produktionens headrar i stället för testets.
   */
  let network: () => Promise<Response> = () =>
    Promise.reject(new TypeError('Inget nät i det här testet'))

  const scopedFetch = () => network()

  /*
   * Function-konstruktorn är hela poängen: den kör den fil som faktiskt levereras, med
   * ett scope vi styr. Alternativet — att importera en kopia av logiken — hade kunnat gå
   * grönt medan `public/sw.js` var trasig, och en service worker är det enda i appen som
   * inte går att återkalla från servern när den väl installerats hos någon.
   *
   * Källan är vår egen, incheckade fil och kommer aldrig utifrån.
   */
  // eslint-disable-next-line @typescript-eslint/no-implied-eval
  const run = new Function('self', 'caches', 'fetch', SW_SOURCE) as (
    s: unknown,
    c: unknown,
    f: unknown,
  ) => void

  run(self, caches, scopedFetch)

  return {
    fetchHandler: listeners['fetch'] as unknown as (event: FetchEvent) => void,
    cache,
    setNetwork: (impl) => {
      network = impl
    },
  }
}

function requestFor(url: string, init?: RequestInit): Request {
  return new Request(url, init)
}

/** Kör fetch-lyssnaren och väntar in svaret, eller null om den lät anropet passera. */
function handle(scope: Scope, request: Request, response: Response): Promise<Response> | null {
  scope.setNetwork(() => Promise.resolve(response))

  let result: Promise<Response> | Response | undefined

  scope.fetchHandler({
    request,
    respondWith: (value) => {
      result = value
    },
  })

  // undefined betyder att service workern lät anropet passera till nätet utan att svara.
  return result === undefined ? null : Promise.resolve(result)
}

beforeEach(() => {
  vi.unstubAllGlobals()
})

describe('service workern cachar aldrig auth-svar', () => {
  it.each([
    ['no-store', 'private, no-store'],
    ['private', 'private'],
    ['bara no-store', 'no-store'],
  ])('sparar inte ett svar märkt %s', async (_name, cacheControl) => {
    // Backend sätter "private, no-store" på allt som inte uttryckligen är publikt. Samma
    // header som håller svaret borta från Vercels edge håller det borta härifrån — så ett
    // auth-svar kan inte hamna i cachen ens om någon glömmer en särskild regel för det.
    const scope = startWorker()
    const response = new Response('{}', {
      status: 200,
      headers: { 'Cache-Control': cacheControl },
    })

    await handle(scope, requestFor('https://karra-matcher.vercel.app/api/v1/me'), response)

    expect(scope.cache.put).not.toHaveBeenCalled()
  })

  it('sparar ett publikt svar', async () => {
    const scope = startWorker()
    const response = new Response('[]', {
      status: 200,
      headers: { 'Cache-Control': 'public, max-age=0, s-maxage=300' },
    })

    await handle(scope, requestFor('https://karra-matcher.vercel.app/api/v1/teams'), response)

    expect(scope.cache.put).toHaveBeenCalledTimes(1)
  })

  it('sparar inte ett felsvar', async () => {
    const scope = startWorker()
    const response = new Response('nej', { status: 500 })

    await handle(scope, requestFor('https://karra-matcher.vercel.app/api/v1/teams'), response)

    expect(scope.cache.put).not.toHaveBeenCalled()
  })
})

describe('service workern rör inte det den inte ska', () => {
  it('lämnar allt utom GET i fred', async () => {
    // En POST som cachas är ett fel som inte går att förklara för någon.
    const scope = startWorker()
    const request = requestFor('https://karra-matcher.vercel.app/api/v1/teams', {
      method: 'POST',
    })

    const result = await handle(scope, request, new Response('{}'))

    expect(result).toBeNull()
  })

  it('lämnar andra origins i fred', async () => {
    // Vädret hämtas direkt från Open-Meteo. En gammal prognos ur vår cache är sämre än
    // ingen prognos alls.
    const scope = startWorker()
    const request = requestFor('https://api.open-meteo.com/v1/forecast?latitude=57')

    expect(await handle(scope, request, new Response('{}'))).toBeNull()
  })

  it('lämnar kalenderfiler i fred', async () => {
    // De laddas ner och hör hemma i telefonens kalender, inte i vår cache.
    const scope = startWorker()
    const request = requestFor('https://karra-matcher.vercel.app/calendar/gul.ics')

    expect(await handle(scope, request, new Response('BEGIN:VCALENDAR'))).toBeNull()
  })
})

describe('service workern gör schemat läsbart utan nät', () => {
  it('svarar ur cachen när nätet är nere', async () => {
    // Täckningen vid fotbollsplanerna är opålitlig (§KM.8). Det här är hela poängen.
    const cached = new Response('[{"slug":"gul"}]')
    const scope = startWorker({ cachedResponse: cached })
    scope.setNetwork(() => Promise.reject(new TypeError('Failed to fetch')))

    let result: Promise<Response> | null = null
    scope.fetchHandler({
      request: requestFor('https://karra-matcher.vercel.app/api/v1/teams'),
      respondWith: (value) => {
        result = value as Promise<Response>
      },
    })

    expect(await (result as unknown as Promise<Response>)).toBe(cached)
  })

  it('kastar vidare när varken nät eller cache finns', async () => {
    // Utan cachat svar ska felet nå appen, som säger till på svenska. Ett tomt svar hade
    // sett ut som "inga matcher".
    const scope = startWorker()
    scope.setNetwork(() => Promise.reject(new TypeError('Failed to fetch')))

    let result: Promise<Response> | null = null
    scope.fetchHandler({
      request: requestFor('https://karra-matcher.vercel.app/api/v1/teams'),
      respondWith: (value) => {
        result = value as Promise<Response>
      },
    })

    await expect(result as unknown as Promise<Response>).rejects.toThrow(TypeError)
  })
})

describe('service workerns källkod', () => {
  it('tar inte över av sig själv vid installation', () => {
    // skipWaiting i install-lyssnaren hade bytt kod under fötterna på någon som läser en
    // matchsida. Den nya versionen ska vänta tills användaren säger till (checklistan 5.5).
    const installBlock = SW_SOURCE.slice(
      SW_SOURCE.indexOf("addEventListener('install'"),
      SW_SOURCE.indexOf("addEventListener('activate'"),
    )

    expect(installBlock).not.toContain('self.skipWaiting()')
  })

  it('har en version att byta när cachen ska slängas', () => {
    expect(SW_SOURCE).toMatch(/const VERSION = '[^']+'/)
  })
})
