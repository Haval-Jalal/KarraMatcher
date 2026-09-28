import { getAuthJson } from '@/lib/api'

/**
 * Adress-förslag medan en admin skriver in en annan plats än hemmaplanen (`#307`).
 *
 * Speglar `GET /api/v1/address-suggestions`. Svaret är bara adress-etiketter (strängar) —
 * aldrig koordinater; positionen härleds server-side när platsen sparas, precis som förr.
 */
export const getAddressSuggestions = (term: string, signal?: AbortSignal): Promise<string[]> =>
  getAuthJson<string[]>(`/api/v1/address-suggestions?q=${encodeURIComponent(term)}`, signal)
