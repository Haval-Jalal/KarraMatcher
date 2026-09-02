import type { Match } from '@/features/matches'

/**
 * Hur nära två avsparkar får ligga innan det räknas som en krock.
 *
 * <h3>Varför två timmar</h3>
 *
 * En match i den här åldersgruppen tar en dryg timme med samling och avslut. Två matcher
 * för <em>samma lag</em> närmare än så är inte ett tight schema utan ett misstag — laget
 * kan inte spela båda.
 *
 * Gränsen är satt så att den inte larmar på det som är avsiktligt. I klubbens riktiga
 * schema ligger fyra lag 75 minuter isär på samma plan; det är planering, inte en krock,
 * och den här vyn visar bara ett lag i taget.
 */
const CLASH_WINDOW_MS = 2 * 60 * 60 * 1000

/**
 * Matcher som krockar med en annan match i samma lista.
 *
 * <para>
 * Inställda matcher räknas inte. En inställd match tar ingen tid i anspråk, och att
 * flagga den hade fått tränaren att leta efter ett problem som inte finns.
 * </para>
 */
export function findClashes(matches: readonly Match[]): ReadonlySet<string> {
  const playing = matches
    .filter((match) => match.status !== 'Cancelled')
    .map((match) => ({ id: match.id, at: new Date(match.kickoffUtc).getTime() }))
    .sort((a, b) => a.at - b.at)

  const clashing = new Set<string>()

  for (let i = 1; i < playing.length; i++) {
    const previous = playing[i - 1]!
    const current = playing[i]!

    if (current.at - previous.at < CLASH_WINDOW_MS) {
      clashing.add(previous.id)
      clashing.add(current.id)
    }
  }

  return clashing
}
