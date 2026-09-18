import { matchDayPosition } from '@/lib/time'

import type { TeamEvent } from './types'

/**
 * Nästa händelse som faktiskt kommer att äga rum, eller null när det inte finns någon.
 *
 * <h3>Vilka hoppas över</h3>
 *
 * **Inställda** är självklara: kortet får inte peka på något som inte blir av.
 *
 * **Framflyttade** hoppas också över — en händelse med status `Postponed` är flyttad
 * *utan nytt datum ännu*, så tiden som står kvar är den gamla. Att lyfta fram den hade
 * varit att med emfas visa fel tid.
 *
 * Dagens händelser räknas som kommande hela dagen: en förälder som öppnar appen på
 * eftermiddagen ska fortfarande se dagens händelse överst.
 */
export function selectNextEvent(
  events: TeamEvent[],
  now: Date | string = new Date(),
): TeamEvent | null {
  const playable = events.filter((event) => event.status === 'Scheduled')

  // Listan kommer sorterad på starttid från API:t, men kortet får inte bero på det:
  // en felsorterad lista hade tyst pekat ut fel händelse.
  const upcoming = playable
    .filter((event) => matchDayPosition(event.kickoffUtc, now) !== 'past')
    .sort((a, b) => a.kickoffUtc.localeCompare(b.kickoffUtc))

  return upcoming[0] ?? null
}
