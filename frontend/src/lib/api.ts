/**
 * Enda vägen ut till API:t (CLAUDE.md → Frontend, Datalager & navigation).
 *
 * Adressen kommer från miljön och aldrig från koden. I drift är den tom, så anropen blir
 * relativa och går genom Vercels rewrite till Render — klienten ser en enda origin, vilket
 * är hela poängen med uppsättningen (§KM.11). Render-URL:en finns bara i `vercel.json`.
 */

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
