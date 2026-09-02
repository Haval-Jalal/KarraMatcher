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
 * Raderar kontot och allt servern äger om det.
 *
 * Sessionen rensas lokalt oavsett utfall — är kontot borta finns inget att vara inloggad
 * på, och står appen kvar som inloggad blir nästa anrop ett obegripligt fel.
 */
export async function deleteAccount(): Promise<void> {
  try {
    await postJson<void>('/api/v1/auth/account', undefined, { method: 'DELETE' })
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
  return claimsFromToken(token)?.email ?? null
}

/**
 * Lagen den inloggade är tränare för, ur token.
 *
 * <h3>Att det här inte är säkerheten</h3>
 *
 * Servern avgör vad någon får göra. Det här avgör bara vad som <em>visas</em> — en tränare
 * ska slippa se knappar för lag hen inte sköter. Den som ändrar värdet i sin egen
 * webbläsare får se en knapp som svarar 403.
 */
export function coachTeamsFromToken(token: string): string[] {
  const claim = claimsFromToken(token)?.coach

  if (typeof claim === 'string') {
    return [claim]
  }

  return Array.isArray(claim) ? claim.filter((slug) => typeof slug === 'string') : []
}

/** Sant om kontot är administratör, alltså tränare för alla lag. */
export function isAdminFromToken(token: string): boolean {
  const claims = claimsFromToken(token)
  const role =
    claims?.role ?? claims?.['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']

  return role === 'admin' || (Array.isArray(role) && role.includes('admin'))
}

interface TokenClaims {
  email?: string
  coach?: string | string[]
  role?: unknown
  [key: string]: unknown
}

function claimsFromToken(token: string): TokenClaims | null {
  try {
    const payload = token.split('.')[1]

    if (payload === undefined) {
      return null
    }

    const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'))
    const claims: unknown = JSON.parse(json)

    return claims !== null && typeof claims === 'object' ? (claims as TokenClaims) : null
  } catch {
    return null
  }
}
