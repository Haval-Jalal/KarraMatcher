import { postJson } from '@/lib/api'
import { clearSession, setAccessToken } from '@/lib/session'

/** Ber servern skicka en kod. Svaret säger aldrig om adressen fanns. */
export async function requestLoginCode(email: string): Promise<void> {
  await postJson<void>('/api/v1/auth/request-code', { email })
}

/** Verifierar koden och startar sessionen. */
export async function verifyLoginCode(email: string, code: string): Promise<void> {
  const session = await postJson<{ accessToken: string }>('/api/v1/auth/verify-code', {
    email,
    code,
  })

  setAccessToken(session.accessToken)
}

/**
 * Loggar ut.
 *
 * Sessionen rensas lokalt även om anropet misslyckas. Servern återkallar hela
 * token-familjen, men lyckas inte det ska knappen ändå ha gjort det den lovade på just
 * den här telefonen.
 */
export async function signOut(): Promise<void> {
  try {
    await postJson<void>('/api/v1/auth/logout')
  } finally {
    clearSession()
  }
}

/**
 * Läser mejladressen ur access-token, för att kunna visa vem som är inloggad.
 *
 * <h3>Att avkoda utan att verifiera är rätt här</h3>
 *
 * Signaturen kontrolleras av servern vid varje anrop. Klienten läser bara innehållet för
 * att visa det — en förfalskad token ger en felaktig text på skärmen och ingenting annat,
 * eftersom den inte öppnar någon dörr. Att verifiera i webbläsaren hade krävt nyckeln,
 * och den ska aldrig lämna servern.
 */
export function emailFromToken(token: string): string | null {
  try {
    const payload = token.split('.')[1]

    if (payload === undefined) {
      return null
    }

    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'))
    const claims: unknown = JSON.parse(json)

    if (claims !== null && typeof claims === 'object' && 'email' in claims) {
      const { email } = claims as { email?: unknown }

      return typeof email === 'string' ? email : null
    }

    return null
  } catch {
    return null
  }
}
