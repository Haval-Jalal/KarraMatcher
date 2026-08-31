/**
 * Enda vägen ut till API:t (CLAUDE.md → Frontend, Datalager & navigation).
 *
 * Adressen kommer från miljön och aldrig från koden. I drift är den tom, så anropen blir
 * relativa och går genom Vercels rewrite till Render — klienten ser en enda origin, vilket
 * är hela poängen med uppsättningen (§KM.11). Render-URL:en finns bara i `vercel.json`.
 */

import { clearSession, getAccessToken, setAccessToken } from '@/lib/session'

const baseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

/** Ett fel som gick att förstå — till skillnad från en krasch. */
export class ApiError extends Error {
  readonly status: number

  /** Sant när anropet aldrig nådde fram. Då är nätet nere, inte servern. */
  readonly offline: boolean

  constructor(message: string, options: { status: number; offline?: boolean }) {
    super(message)
    this.name = 'ApiError'
    this.status = options.status
    this.offline = options.offline ?? false
  }
}

/** Plockar ut ett begripligt meddelande ur ett ProblemDetails-svar. */
async function messageFor(response: Response): Promise<string> {
  try {
    const problem: unknown = await response.json()

    if (problem !== null && typeof problem === 'object') {
      const { title, detail } = problem as { title?: unknown; detail?: unknown }
      const parts = [title, detail].filter((part): part is string => typeof part === 'string')

      if (parts.length > 0) {
        return parts.join(' — ')
      }
    }
  } catch {
    // Svaret var inte JSON. Faller tillbaka på statuskoden nedan.
  }

  return `Servern svarade ${String(response.status)}.`
}

/**
 * Anti-forgery-token, hämtad en gång och sparad i minnet.
 *
 * Servern kräver den på allt som ändrar tillstånd, eftersom refresh-token ligger i en
 * cookie. Den är ingen hemlighet — halva poängen är att den ska gå att läsa av vår egen
 * sida men inte av någon annans.
 */
let csrfToken: string | null = null

async function getCsrfToken(): Promise<string> {
  csrfToken ??= (await getJson<{ token: string }>('/api/v1/auth/csrf')).token

  return csrfToken
}

/**
 * Skickar något som ändrar tillstånd.
 *
 * <h3>401 hanteras här och ingen annanstans</h3>
 *
 * Får vi 401 på ett anrop som bar en access-token har den hunnit gå ut — den lever en
 * kvart. Då förnyas sessionen mot cookien och anropet görs om **en** gång. Lyckas inte
 * det är sessionen slut, och då rensas den.
 *
 * Att ha den logiken på ett ställe är hela poängen: varje vy som själv försökte hantera
 * 401 skulle förr eller senare hantera det olika.
 */
export async function postJson<T>(
  path: string,
  body?: unknown,
  options: { retryOnUnauthorized?: boolean } = {},
): Promise<T> {
  const retry = options.retryOnUnauthorized ?? true
  const token = getAccessToken()

  let response: Response

  try {
    response = await fetch(`${baseUrl}${path}`, {
      method: 'POST',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        'X-CSRF-TOKEN': await getCsrfToken(),
        ...(token === null ? {} : { Authorization: `Bearer ${token}` }),
      },
      // Refresh-cookien är förstapart tack vare Vercel-rewriten (§KM.11). Explicit,
      // så att ingen tar bort den i tron att den är överflödig.
      credentials: 'same-origin',
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    })
  } catch {
    throw new ApiError('Ingen anslutning till servern.', { status: 0, offline: true })
  }

  if (response.status === 401 && retry && token !== null) {
    const renewed = await renewSession()

    if (renewed) {
      return postJson<T>(path, body, { retryOnUnauthorized: false })
    }
  }

  if (!response.ok) {
    throw new ApiError(await messageFor(response), { status: response.status })
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

/**
 * Pågående förnyelse, om någon.
 *
 * <h3>Varför förnyelsen måste vara enkelspårig</h3>
 *
 * Servern **roterar** refresh-token: varje förnyelse ger en ny och märker den gamla som
 * använd. Dyker den gamla upp igen tolkas det som en stöld och **hela sessionsfamiljen
 * återkallas** — vilket är precis vad som ska hända när någon kopierat en token.
 *
 * Två samtidiga förnyelser från vår egen klient ser likadana ut. Skulle appen råka
 * skicka två — två anrop som får 401 samtidigt, eller en skyddad route och
 * AuthProvider som startar samtidigt — hade den alltså loggat ut användaren själv, och
 * felet hade sett ut som ett serverfel.
 *
 * Därför delar alla som frågar på samma anrop.
 */
let pendingRenewal: Promise<boolean> | null = null

/**
 * Byter refresh-cookien mot en ny access-token.
 *
 * Svarar med om det gick, i stället för att kasta: ett misslyckande är det normala för
 * den som aldrig loggat in, och ett undantag för något normalt gör koden svårare att läsa.
 */
export function renewSession(): Promise<boolean> {
  pendingRenewal ??= (async () => {
    try {
      const session = await postJson<{ accessToken: string }>('/api/v1/auth/refresh', undefined, {
        retryOnUnauthorized: false,
      })

      setAccessToken(session.accessToken)

      return true
    } catch {
      clearSession()

      return false
    } finally {
      pendingRenewal = null
    }
  })()

  return pendingRenewal
}

export async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  let response: Response

  try {
    response = await fetch(`${baseUrl}${path}`, {
      headers: { Accept: 'application/json' },
      ...(signal ? { signal } : {}),
    })
  } catch {
    // fetch kastar bara när anropet inte gick att genomföra alls: nätet är nere, eller
    // servern gick inte att nå. Ett HTTP-fel hamnar aldrig här — det syns på response.ok.
    throw new ApiError('Ingen anslutning till servern.', { status: 0, offline: true })
  }

  if (!response.ok) {
    throw new ApiError(await messageFor(response), { status: response.status })
  }

  return (await response.json()) as T
}
