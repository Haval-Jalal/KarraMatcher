import { useQuery } from '@tanstack/react-query'

import { ApiError, getJson } from '@/lib/api'

import type { EventDetail } from './types'

export const eventQueryKey = (id: string) => ['event', id] as const

/**
 * En enskild händelse med spelplats, koordinater och lag.
 *
 * Stängd i v2 (§KM.3): kräver inloggning och medlemskap i händelsens lag.
 *
 * Routen är bakom `requireSession`, så sessionen är redan förnyad när queryn körs. Vi gör bara om
 * ett äkta serverfel (5xx, kan vara övergående). Ett 4xx är ett äkta svar (403 saknad behörighet,
 * 404 borttagen) och blir inte rätt av ett omförsök — och offline visar hellre beskedet direkt med
 * en manuell "Försök igen" än att dröja bakom tysta omförsök. Så slipper användaren vänta och
 * Render väcks inte i onödan (§KM.11, #400).
 */
export function useEvent(id: string) {
  return useQuery({
    queryKey: eventQueryKey(id),
    queryFn: ({ signal }) => getJson<EventDetail>(`/api/v1/events/${id}`, signal),
    retry: (failureCount, error) =>
      error instanceof ApiError && error.status >= 500 && failureCount < 2,
  })
}
