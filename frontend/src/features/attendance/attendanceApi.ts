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

/** Ett svar så som tränaren ser det i summeringen. Namnet är en vuxens (`#154`). */
export interface AttendanceResponder {
  /** Svarets id — en stabil nyckel för listan, inte kontots id. */
  id: string
  /** Den svarande vuxnas namn, eller null om hen inte fyllt i något. Aldrig ett barn. */
  name: string | null
  status: AttendanceStatus
  count: number
}

/**
 * Tränarens summering för en match. Speglar `AttendanceSummaryDto`.
 *
 * Ingen lista över dem som *inte* svarat — den kräver en förälder↔lag-koppling som inte
 * finns (§KM.1) och hör hemma i `#63`. Summeringen räknar bara dem som svarat.
 */
export interface AttendanceSummary {
  /** Summan av antalen från dem som svarat Kommer — hur många som faktiskt dyker upp. */
  comingPeople: number
  maybePeople: number
  cantComeFamilies: number
  respondedFamilies: number
  responders: AttendanceResponder[]
  /**
   * Hur många som förväntas svara men inte gjort det — lagets prenumeranter med konto minus
   * dem som svarat. Kan nås av en påminnelse.
   */
  notAnsweredCount: number
  /** Namnen på dem som inte svarat och fyllt i ett namn. Aldrig ett barn. */
  notAnsweredNames: string[]
}

/**
 * Tränarens summering. Kräver tränarskap för laget (laget står i adressen), och bär de
 * svarande vuxnas namn — därför aldrig för en gäst och aldrig i en delad cache.
 */
export function getAttendanceSummary(
  teamSlug: string,
  matchId: string,
  signal?: AbortSignal,
): Promise<AttendanceSummary> {
  return getAuthJson<AttendanceSummary>(
    `/api/v1/teams/${encodeURIComponent(teamSlug)}/matches/${encodeURIComponent(matchId)}/attendance/summary`,
    signal,
  )
}

/**
 * Påminner dem som inte svarat. Notisen går bara till lagets prenumeranter med konto som
 * ännu inte svarat — aldrig till någon som redan svarat. Svarar med antalet som påmindes.
 */
export function remindNonResponders(
  teamSlug: string,
  matchId: string,
): Promise<{ reminded: number }> {
  return postJson<{ reminded: number }>(
    `/api/v1/teams/${encodeURIComponent(teamSlug)}/matches/${encodeURIComponent(matchId)}/attendance/remind`,
  )
}
