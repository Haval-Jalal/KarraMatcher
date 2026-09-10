import { useQuery } from '@tanstack/react-query'

import { listOffers, listRequests, type CarpoolOffer, type CarpoolRequest } from './carpoolApi'

export const carpoolOffersQueryKey = (matchId: string) => ['carpool', matchId] as const

export const carpoolRequestsQueryKey = (matchId: string, offerId: string) =>
  ['carpool', matchId, 'requests', offerId] as const

/**
 * Matchens erbjudanden.
 *
 * <h3>Kortare färskhet än resten av appen</h3>
 *
 * Standarden är en minut, satt för ett matchschema som ändras sällan. Samåkning ändras
 * i stället under den halvtimme då folk gör sig i ordning, och en plats som redan är
 * tagen ska inte se ledig ut. Därför hämtas listan om vid varje montering.
 */
export function useCarpoolOffers(matchId: string) {
  return useQuery({
    queryKey: carpoolOffersQueryKey(matchId),
    queryFn: ({ signal }) => listOffers(matchId, signal),
    staleTime: 0,
  })
}

/**
 * Förfrågningarna på ett erbjudande.
 *
 * Kräver inloggning, så hämtningen är avstängd för en gäst — annars hade varje gäst som
 * öppnade en match kostat ett 401 och en väckt Render.
 */
export function useCarpoolRequests(matchId: string, offerId: string, enabled: boolean) {
  return useQuery({
    queryKey: carpoolRequestsQueryKey(matchId, offerId),
    queryFn: ({ signal }) => listRequests(matchId, offerId, signal),
    enabled,
    staleTime: 0,
  })
}

export type { CarpoolOffer, CarpoolRequest }
