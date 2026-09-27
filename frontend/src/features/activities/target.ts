import type { EventInput } from '@/features/admin/adminApi'
import type { Roster } from '@/features/children'

/**
 * Kallelse-målgruppen för en aktivitet (§KM.7, `#333`).
 *
 * Tre lägen, alla på match och träning: hela truppen, valda lag, eller namngivna barn. Cup och
 * övrigt har ingen vanlig kallelse här (cupens platstak-kallelse är ett eget steg, `#335`;
 * övriga händelser kallar aldrig), så väljaren visas bara för match och träning.
 */
export type TargetMode = 'trupp' | 'lag' | 'children'

/** Vald målgrupp: läget plus urvalet det läget använder. */
export interface KallelseTarget {
  mode: TargetMode
  teamIds: string[]
  childIds: string[]
}

/** Vilka typer som har en vanlig kallelse-målgrupp (match och träning). */
export function typeHasTarget(type: EventInput['type']): boolean {
  return type === 'Match' || type === 'Training'
}

/**
 * Förval per typ (`#333`): match → välj lag, träning → hela truppen. Cup/övrigt saknar målgrupp
 * här, men ges ett neutralt utgångsläge så state alltid är definierat.
 */
export function defaultTargetFor(type: EventInput['type']): KallelseTarget {
  return {
    mode: type === 'Match' ? 'lag' : 'trupp',
    teamIds: [],
    childIds: [],
  }
}

/**
 * Löser en målgrupp mot truppens uppställning till två saker: händelsens lag-märke och de barn
 * som ska kallas.
 *
 * <ul>
 *   <li><b>Hela truppen</b> → trupp-vid händelse (inget lag), kallelse till alla barn i truppen.</li>
 *   <li><b>Valda lag</b> → händelsen märks med laget <em>när exakt ett</em> valts (så en match
 *       syns i det lagets schema); flera lag = trupp-vid. Kallelse till de lagens barn.</li>
 *   <li><b>Namngivna barn</b> → trupp-vid händelse, kallelse till de utvalda barnen.</li>
 * </ul>
 */
export function resolveTarget(
  target: KallelseTarget,
  roster: Roster,
): { eventTeamId: string | null; childIds: string[] } {
  if (target.mode === 'trupp') {
    return { eventTeamId: null, childIds: roster.children.map((child) => child.id) }
  }

  if (target.mode === 'lag') {
    const teams = new Set(target.teamIds)

    return {
      eventTeamId: target.teamIds.length === 1 ? target.teamIds[0]! : null,
      childIds: roster.children
        .filter((child) => child.teamId !== null && teams.has(child.teamId))
        .map((child) => child.id),
    }
  }

  const chosen = new Set(target.childIds)

  return {
    eventTeamId: null,
    childIds: roster.children.filter((child) => chosen.has(child.id)).map((child) => child.id),
  }
}
