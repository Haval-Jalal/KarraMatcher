import type { EventInput } from '@/features/admin/adminApi'
import type { TeamEvent } from '@/features/events'
import { getJson, postJson } from '@/lib/api'

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

/**
 * En aktivitet i trupp-listan (`#334`): händelsen som appen visar den, plus dess lag — eller
 * `null` för en trupp-vid händelse (ingen lagfärg, `#332`). Speglar backendens `TruppActivityDto`.
 */
export interface Activity {
  event: TeamEvent
  team: { slug: string; name: string; ageGroup: string; colorHex: string } | null
}

/** Truppens alla aktiviteter (alla typer, alla lag + trupp-vida), i avsparksordning. */
export function getTruppActivities(truppId: string): Promise<Activity[]> {
  return getJson<Activity[]>(`/api/v1/trupper/${encodeURIComponent(truppId)}/events`)
}
