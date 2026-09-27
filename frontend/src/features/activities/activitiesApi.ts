import type { EventInput } from '@/features/admin/adminApi'
import { postJson } from '@/lib/api'

/**
 * Adminens skapande av en aktivitet på trupp-nivå (`#333`, epic #330).
 *
 * Till skillnad från tränarens lag-väg (`adminApi.createEvent`, som hänger på lagets slug) skapar
 * den här på hela truppen: `teamId` är valfritt — `null` = trupp-vid händelse (hör bara till
 * truppen), ett satt lag = en lag-riktad händelse (som måste tillhöra truppen; det vaktas
 * server-side, §KM.7). Behörigheten prövas mot truppen (`AdminOfTrupp`).
 */

/** Fälten som skickas för en aktivitet — händelsens fält plus dess valfria lag-märke. */
export interface CreateActivityInput extends EventInput {
  /** `null` = trupp-vid (hela truppen); ett lag-id = lag-riktad (laget måste höra till truppen). */
  teamId: string | null
}

/** Det enda vi behöver ur svaret: den skapade händelsens id (för att sedan sätta kallelsen). */
export interface CreatedActivity {
  id: string
}

/** Skapar aktiviteten i truppen och ger tillbaka dess id. */
export function createTruppEvent(
  truppId: string,
  input: CreateActivityInput,
): Promise<CreatedActivity> {
  return postJson<CreatedActivity>(
    `/api/v1/admin/trupper/${encodeURIComponent(truppId)}/events`,
    input,
  )
}
