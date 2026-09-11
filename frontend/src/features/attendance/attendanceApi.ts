import { getAuthJson, postJson } from '@/lib/api'

/**
 * Kallelsen och närvarosvaren mot API:t (`#57`, §KM.7).
 *
 * <h3>Inget barn passerar</h3>
 *
 * En vuxen svarar för sin familj med ett antal — aldrig ett namn. Det finns med avsikt
 * ingen typ här som beskriver ett barn (§KM.1): servern har inga sådana uppgifter, och
 * klienten skickar inga.
 *
 * <h3>Bakom grinden</h3>
 *
 * Är kallelsen avslagen för laget svarar servern `404` (§KM.7). Läget hämtas därför bara
 * för en inloggad läsare, och en `404` betyder "funktionen finns inte här" — inte ett fel.
 */

/** Hur en vuxen svarar. Speglar `AttendanceStatus` i backend. */
export type AttendanceStatus = 'Coming' | 'CantCome' | 'Maybe'

/** Taket för hur många ur en familj som kan anges — som i backend. */
export const MAX_ATTENDANCE_COUNT = 4

/** Ett eget svar så som API:t levererar det. */
export interface AttendanceResponse {
  status: AttendanceStatus
  count: number
  /** När svaret senast ändrades, i UTC. */
  updatedUtc: string
}

/** Läget för en inloggad vuxen på en match. Speglar `AttendanceStateDto`. */
export interface AttendanceState {
  /** Sant när tränaren har kallat. Är den falsk finns inget att svara på än. */
  callOpen: boolean
  /** Avspark i UTC. Efter den går svaret inte längre att ändra. */
  kickoffUtc: string
  /** Mitt eget svar, eller null om jag inte svarat än. */
  myResponse: AttendanceResponse | null
}

const base = (matchId: string) => `/api/v1/matches/${encodeURIComponent(matchId)}/attendance`

/**
 * Mitt eget läge på en match.
 *
 * Kräver konto och ligger bakom grinden. En `404` betyder att kallelsen är avslagen för
 * laget — anroparen renderar då ingenting, i stället för ett fel.
 */
export function getAttendanceState(
  matchId: string,
  signal?: AbortSignal,
): Promise<AttendanceState> {
  return getAuthJson<AttendanceState>(base(matchId), signal)
}

/**
 * Tränaren kallar till matchen.
 *
 * Laget står i adressen — behörigheten prövas mot slugen, det finns inget lagfält att
 * skicka.
 */
export function openAttendanceCall(teamSlug: string, matchId: string): Promise<void> {
  return postJson<void>(
    `/api/v1/teams/${encodeURIComponent(teamSlug)}/matches/${encodeURIComponent(matchId)}/attendance/call`,
  )
}

/** Sparar eller ändrar mitt svar. Går att ändra ända fram till avspark. */
export function submitAttendanceResponse(
  matchId: string,
  status: AttendanceStatus,
  count: number,
): Promise<void> {
  return postJson<void>(`${base(matchId)}/response`, { status, count }, { method: 'PUT' })
}
