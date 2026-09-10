import { getJson, postJson } from '@/lib/api'

/**
 * Samåkningen mot API:t (§KM.12).
 *
 * <h3>Vad som medvetet inte finns här</h3>
 *
 * Inget förarfält och inget telefonnummer. Föraren <em>är</em> den inloggade, och
 * överenskommelsen sker i meddelandefältet — appen frågar aldrig efter ett nummer.
 * Fritexten är potentiell PII och får bara nå de inblandade, så den loggas aldrig och
 * cachas aldrig i en delad cache.
 */

/** Vilket håll föraren kör. Speglar `CarpoolDirection` i backend. */
export type CarpoolDirection = 'ToMatch' | 'FromMatch' | 'Both'

/** Förfrågans tillstånd. Speglar `CarpoolRequestStatus` i backend. */
export type CarpoolRequestStatus = 'Pending' | 'Accepted' | 'Denied' | 'Retracted'

/** Taket för hur många som får plats utöver föraren och det egna barnet. */
export const MAX_SEATS = 4

/** Taket för fritext, samma åt båda hållen — som i backend. */
export const MAX_MESSAGE_LENGTH = 500

/** Ett erbjudande så som API:t levererar det. Speglar `CarpoolOfferDto`. */
export interface CarpoolOffer {
  id: string
  matchId: string
  direction: CarpoolDirection
  departurePlace: string
  /** Avgång i UTC. Konverteras till svensk tid i `@/lib/time`, aldrig här (§KM.5). */
  departureUtc: string
  seats: number
  seatsTaken: number
  seatsLeft: number
  /**
   * Sant när platserna tagit slut — men erbjudandet är inte stängt.
   * Det går fortfarande att fråga, så att föraren kan svara i stället för att den som
   * frågar möts av en död knapp (§KM.12).
   */
  isFull: boolean
  /** Förarens egen notis. Följer bara med till den som är inloggad. */
  note: string | null
  isMine: boolean
}

/** En förfrågan så som API:t levererar den. Speglar `CarpoolRequestDto`. */
export interface CarpoolRequest {
  id: string
  offerId: string
  seats: number
  message: string | null
  responseMessage: string | null
  status: CarpoolRequestStatus
  createdUtc: string
  isMine: boolean
}

/** Det föraren fyller i. Tiden är redan omräknad till UTC. */
export interface CarpoolOfferInput {
  direction: CarpoolDirection
  departurePlace: string
  departureUtc: string
  seats: number
  note: string | null
}

/** Det den som frågar fyller i. */
export interface CarpoolRequestInput {
  seats: number
  message: string | null
}

const base = (matchId: string) => `/api/v1/matches/${encodeURIComponent(matchId)}/carpool`

/**
 * Matchens öppna erbjudanden.
 *
 * Öppen för alla (§KM.3), men aldrig edge-cachad: en inloggad ser förarens notis och en
 * gäst gör det inte, så svaret skiljer sig åt mellan läsare.
 */
export function listOffers(matchId: string, signal?: AbortSignal): Promise<CarpoolOffer[]> {
  return getJson<CarpoolOffer[]>(`${base(matchId)}/offers`, signal)
}

export function createOffer(matchId: string, input: CarpoolOfferInput): Promise<CarpoolOffer> {
  return postJson<CarpoolOffer>(`${base(matchId)}/offers`, input)
}

/**
 * Drar tillbaka ett erbjudande.
 *
 * Det raderas inte — den som frågat ska kunna se vad som hände med sin förfrågan.
 */
export function withdrawOffer(matchId: string, offerId: string): Promise<void> {
  return postJson<void>(`${base(matchId)}/offers/${offerId}/withdraw`)
}

export function askForSeat(
  matchId: string,
  offerId: string,
  input: CarpoolRequestInput,
): Promise<CarpoolRequest> {
  return postJson<CarpoolRequest>(`${base(matchId)}/offers/${offerId}/requests`, input)
}

/**
 * Förfrågningarna på ett erbjudande.
 *
 * Föraren får alla — det är hen som ska svara. Alla andra får bara sina egna, och den
 * filtreringen sitter i servern, inte här.
 */
export function listRequests(
  matchId: string,
  offerId: string,
  signal?: AbortSignal,
): Promise<CarpoolRequest[]> {
  return getJson<CarpoolRequest[]>(`${base(matchId)}/offers/${offerId}/requests`, signal)
}

/** Återtar en egen förfrågan. */
export function retractRequest(matchId: string, requestId: string): Promise<void> {
  return postJson<void>(`${base(matchId)}/requests/${requestId}/retract`)
}

/** Föraren säger ja. Meddelandet är valfritt — ett ja behöver inga ord. */
export function acceptRequest(
  matchId: string,
  requestId: string,
  message: string | null,
): Promise<void> {
  return postJson<void>(`${base(matchId)}/requests/${requestId}/accept`, { message })
}

/**
 * Föraren säger nej.
 *
 * Meddelandet är obligatoriskt (§KM.12) och kravet finns även server-side. Formuläret
 * håller det bara borta från en onödig vända till servern.
 */
export function denyRequest(matchId: string, requestId: string, message: string): Promise<void> {
  return postJson<void>(`${base(matchId)}/requests/${requestId}/deny`, { message })
}
