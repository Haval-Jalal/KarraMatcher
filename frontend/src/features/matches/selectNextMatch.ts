import { matchDayPosition } from '@/lib/time'

import type { Match } from './types'

/**
 * Nästa match som faktiskt kommer att spelas, eller null när säsongen är slut.
 *
 * <h3>Vilka matcher som hoppas över</h3>
 *
 * **Inställda** är självklara: kortet får inte peka på en match som inte blir av.
 *
 * **Framflyttade** hoppas också över, trots att issuet bara nämner inställda. En match med
 * status `Postponed` är flyttad *utan nytt datum ännu* — tiden som står kvar är den gamla.
 * Att lyfta fram den som "nästa match" hade varit att med emfas visa fel tid, vilket är
 * sämre än att visa nästa match som faktiskt gäller.
 *
 * Dagens matcher räknas som kommande hela dagen, av samma skäl som i matchlistan: en
 * förälder som öppnar appen på eftermiddagen ska fortfarande se dagens match överst.
 */
export function selectNextMatch(matches: Match[], now: Date | string = new Date()): Match | null {
  const playable = matches.filter((match) => match.status === 'Scheduled')

  // Listan kommer sorterad på avspark från API:t, men kortet får inte bero på det:
  // en felsorterad lista hade tyst pekat ut fel match.
  const upcoming = playable
    .filter((match) => matchDayPosition(match.kickoffUtc, now) !== 'past')
    .sort((a, b) => a.kickoffUtc.localeCompare(b.kickoffUtc))

  return upcoming[0] ?? null
}
