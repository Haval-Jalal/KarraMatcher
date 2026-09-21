import { getAuthJson, postJson } from '@/lib/api'

/**
 * Den riktade kallelsen per barn (§KM.7, `#199`).
 *
 * <h3>Per barn, Ja/Nej</h3>
 *
 * En kallelse riktas mot utvalda barn ur hela truppen (ett lag fylls på med barn ur andra
 * lag vid behov). En vårdnadshavare svarar per barn: Ja (Coming) eller Nej (NotComing) —
 * inget "kanske". Barn visas som "Liam J" (§KM.1), aldrig hela efternamnet.
 *
 * <h3>Bakom grinden</h3>
 *
 * Är kallelsen avslagen för laget svarar servern `404` på vårdnadshavarens vy (§KM.7) — då
 * renderar anroparen ingenting, i stället för ett fel.
 */

/** Hur en vårdnadshavare svarar för ett barn. Speglar `AttendanceReply` i backend. */
export type AttendanceReply = 'Coming' | 'NotComing'

/** Ett av mina barn i en kallelse, med mitt svar. */
export interface MyChildInvitation {
  childId: string
  /** Barnets namn i minimal form: "Liam J". */
  displayName: string
  /** Mitt svar, eller null om jag inte svarat än. */
  reply: AttendanceReply | null
}

/** Vårdnadshavarens vy av en kallelse. Speglar `MyKallelseDto`. */
export interface MyKallelse {
  /** Sant när en kallelse har öppnats för händelsen. */
  callOpen: boolean
  /** Starttid i UTC. Efter den går svaret inte längre att ändra. */
  kickoffUtc: string
  /** Mina egna kallade barn. */
  children: MyChildInvitation[]
}

/** Ett kallat barn i adminens sammanställning. Speglar `KallelseChildDto`. */
export interface KallelseChild {
  childId: string
  /** "Liam J" — aldrig hela efternamnet (§KM.1). */
  displayName: string
  /** Barnets lag, för att visa varifrån en inkallad kommer. */
  teamName: string | null
  colorHex: string | null
  reply: AttendanceReply | null
}

/** Adminens sammanställning av en kallelse. Speglar `KallelseSummaryDto`. */
export interface KallelseSummary {
  callOpen: boolean
  /** Antal barn som svarat Ja. */
  coming: number
  /** Antal barn som svarat Nej. */
  notComing: number
  /** Antal kallade barn utan svar. */
  notAnswered: number
  children: KallelseChild[]
}

const guardianBase = (eventId: string) => `/api/v1/events/${encodeURIComponent(eventId)}/kallelse`

const adminBase = (truppId: string, eventId: string) =>
  `/api/v1/admin/trupper/${encodeURIComponent(truppId)}/events/${encodeURIComponent(eventId)}/kallelse`

/**
 * Mina egna kallade barn för en händelse.
 *
 * Kräver konto. En `404` betyder att kallelsen är avslagen för laget (§KM.7) — anroparen
 * renderar då ingenting, i stället för ett fel.
 */
export function getMyKallelse(eventId: string, signal?: AbortSignal): Promise<MyKallelse> {
  return getAuthJson<MyKallelse>(guardianBase(eventId), signal)
}

/** Sparar eller ändrar mitt svar för ett barn. Går att ändra ända fram till avspark. */
export function respond(eventId: string, childId: string, reply: AttendanceReply): Promise<void> {
  return postJson<void>(
    `${guardianBase(eventId)}/children/${encodeURIComponent(childId)}`,
    { reply },
    { method: 'PUT' },
  )
}

/**
 * Adminens sammanställning: kallade barn med lag och svar, samt Ja/Nej/ej-svarat-antal.
 *
 * Kräver admin för truppen (truppens id står i adressen). Bär barnens namn, därför aldrig
 * för en gäst och aldrig i en delad cache.
 */
export function getKallelseSummary(
  truppId: string,
  eventId: string,
  signal?: AbortSignal,
): Promise<KallelseSummary> {
  return getAuthJson<KallelseSummary>(adminBase(truppId, eventId), signal)
}

/**
 * Skickar eller uppdaterar kallelsen: vilka barn ur truppen som kallas. Full synk — mängden
 * ersätter den föregående (redan kallade barn behåller sitt svar server-side).
 */
export function setKallelse(truppId: string, eventId: string, childIds: string[]): Promise<void> {
  return postJson<void>(adminBase(truppId, eventId), { childIds }, { method: 'PUT' })
}

/** Påminner vårdnadshavarna till de kallade barn som inte svarat. Svarar med antalet. */
export function remind(truppId: string, eventId: string): Promise<{ reminded: number }> {
  return postJson<{ reminded: number }>(`${adminBase(truppId, eventId)}/remind`)
}
