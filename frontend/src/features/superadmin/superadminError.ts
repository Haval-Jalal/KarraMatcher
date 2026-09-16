import { ApiError } from '@/lib/api'

/**
 * Ett begripligt svenskt felmeddelande för superadmin-konsolen (`#192`).
 *
 * Servern svarar med ProblemDetails (t.ex. "Upptaget" eller "Referens saknas"), och
 * <c>ApiError.message</c> bär den texten — den visas rakt av. Ett nätfel får sitt eget,
 * och allt annat en neutral fallback.
 */
export function superadminError(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Kontrollera nätet och försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}
