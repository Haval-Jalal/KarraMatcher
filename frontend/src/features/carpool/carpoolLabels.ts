import type { CarpoolDirection, CarpoolOffer, CarpoolRequestStatus } from './carpoolApi'

/**
 * Orden samåkningen använder.
 *
 * <h3>Varför de ligger samlade</h3>
 *
 * Samma sak sägs på flera ställen: i kortet, i förarens lista och i bekräftelsen. Sägs
 * det olika låter appen som två olika appar. Texterna är dessutom det som testerna letar
 * efter — de är gränssnittet, sett från en förälder.
 */

/** Vilket håll föraren kör, skrivet som en förälder skulle säga det. */
export function directionLabel(direction: CarpoolDirection): string {
  switch (direction) {
    case 'ToMatch':
      return 'Till matchen'
    case 'FromMatch':
      return 'Hem från matchen'
    default:
      return 'Både till och hem'
  }
}

/**
 * Hur många som får plats.
 *
 * Fullt sägs rakt ut, för det ändrar vad man gör härnäst. Att bara skriva "0 platser
 * kvar" hade tvingat läsaren att räkna ut det själv.
 */
export function seatsLabel(offer: CarpoolOffer): string {
  if (offer.isFull) {
    return 'Fullt'
  }

  return offer.seatsLeft === 1 ? '1 plats kvar' : `${String(offer.seatsLeft)} platser kvar`
}

/** Antal platser i en förfrågan, i ord. */
export function seatCountLabel(seats: number): string {
  return seats === 1 ? '1 plats' : `${String(seats)} platser`
}

/** Vad som hänt med en förfrågan. */
export function requestStatusLabel(status: CarpoolRequestStatus): string {
  switch (status) {
    case 'Accepted':
      return 'Accepterad'
    case 'Denied':
      return 'Nekad'
    case 'Retracted':
      return 'Återtagen'
    default:
      return 'Väntar på svar'
  }
}

/**
 * Färdiga formuleringar för ett nekande (§KM.12).
 *
 * <h3>Varför de finns</h3>
 *
 * Ett tyst nej får inte förekomma — det är en granne man möter på planen nästa lördag.
 * Men att formulera ett nej är just det som får någon att skjuta upp svaret tills det är
 * för sent. Färdiga meningar gör det till ett tryck, och fritexten finns kvar för den som
 * vill säga något eget.
 */
export const DENIAL_PHRASES = [
  'Ändrade planer, kan tyvärr inte köra.',
  'Någon annan hann före.',
  'Bilen är tyvärr full.',
] as const
