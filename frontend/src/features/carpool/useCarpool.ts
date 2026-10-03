import { useQuery } from '@tanstack/react-query'

import {
  listOffers,
  listRequests,
  listRideOffers,
  listRideRequests,
  listTeamCarpool,
  type CarpoolOffer,
  type CarpoolRequest,
  type CarpoolRideOffer,
  type CarpoolRideRequest,
  type TeamCarpoolMatch,
} from './carpoolApi'

export const carpoolOffersQueryKey = (matchId: string) => ['carpool', matchId] as const

export const carpoolRequestsQueryKey = (matchId: string, offerId: string) =>
  ['carpool', matchId, 'requests', offerId] as const

export const carpoolRideRequestsQueryKey = (matchId: string) =>
  ['carpool', matchId, 'ride-requests'] as const

export const carpoolRideOffersQueryKey = (matchId: string, rideRequestId: string) =>
  ['carpool', matchId, 'ride-requests', rideRequestId, 'offers'] as const

export const teamCarpoolQueryKey = (slug: string) => ['carpool', 'team', slug] as const

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

/**
 * Matchens öppna skjutsförfrågningar (`#63`).
 *
 * Samma korta färskhet som erbjudandena: en förfrågan som redan blivit löst eller tillbakadragen
 * ska inte ligga kvar och se öppen ut medan folk gör sig i ordning.
 */
export function useCarpoolRideRequests(matchId: string) {
  return useQuery({
    queryKey: carpoolRideRequestsQueryKey(matchId),
    queryFn: ({ signal }) => listRideRequests(matchId, signal),
    staleTime: 0,
  })
}

/**
 * Platserbjudandena på en skjutsförfrågan.
 *
 * Den som frågade ser alla, en förare bara sitt eget — filtreringen sker i servern (§KM.12).
 */
export function useCarpoolRideOffers(matchId: string, rideRequestId: string, enabled: boolean) {
  return useQuery({
    queryKey: carpoolRideOffersQueryKey(matchId, rideRequestId),
    queryFn: ({ signal }) => listRideOffers(matchId, rideRequestId, signal),
    enabled,
    staleTime: 0,
  })
}

/**
 * Lagets samåkning för tränaren.
 *
 * Samma korta färskhet som matchens egen lista: överblicken läses inför en helg, och en
 * plats som redan är tagen ska inte se ledig ut.
 */
export function useTeamCarpool(slug: string, enabled: boolean) {
  return useQuery({
    queryKey: teamCarpoolQueryKey(slug),
    queryFn: ({ signal }) => listTeamCarpool(slug, signal),
    enabled,
    staleTime: 0,
  })
}

export type { CarpoolOffer, CarpoolRequest, CarpoolRideOffer, CarpoolRideRequest, TeamCarpoolMatch }
