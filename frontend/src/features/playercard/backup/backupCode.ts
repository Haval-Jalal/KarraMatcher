import { markEarnedAsSeen } from '../badges/badges'
import { migrate } from '../storage/playerCardStore'
import { CURRENT_VERSION, emptyCard, type PlayerCardData } from '../storage/schema'

/**
 * Säkerhetskopian som en kod att kopiera.
 *
 * <h3>Varför det här är en förstaklassfunktion</h3>
 *
 * Spelarkortet finns bara på telefonen (§KM.2). Den här koden är därför det <b>enda</b>
 * som står mellan en familj och en förlorad säsong vid ett telefonbyte — inte en
 * extrafiness utan förutsättningen för att modellen ska vara försvarbar.
 *
 * <h3>Formatet</h3>
 *
 * <c>KARRA2.</c> följt av base64 av JSON. Prefixet gör att en klistrad kod går att känna
 * igen direkt, i stället för att felet upptäcks först när JSON-tolkningen misslyckas.
 *
 * <h3>Den gamla koden läses också</h3>
 *
 * Föregångarens <c>KARRA1.</c> har formen
 * <c>{ kids: [{id, name, team}], stats: { "datum_lag_motståndare": { us, them, kids: { id: {g, a} } } } }</c>.
 * Det är avläst ur den app familjerna använder i dag, inte antaget — en gissning här hade
 * inte upptäckts förrän någon försökte flytta över en riktig säsong och misslyckades.
 */

const PREFIX = 'KARRA2.'
const LEGACY_PREFIX = 'KARRA1.'

/** Kodar kortet till en kod. */
export function encodeBackup(card: PlayerCardData): string {
  return PREFIX + toBase64(JSON.stringify({ ...card, version: CURRENT_VERSION }))
}

/** Vad en avkodning gav. */
export type DecodeResult =
  { ok: true; card: PlayerCardData; legacy: boolean } | { ok: false; reason: string }

/**
 * Läser en kod.
 *
 * <para>
 * Svarar med ett begripligt skäl i stället för att kasta. Den som klistrat in fel sak ska
 * få veta <em>vad</em> som var fel — en kod som saknar prefix är något annat än en kod som
 * är avhuggen, och råden skiljer sig.
 * </para>
 */
export function decodeBackup(raw: string): DecodeResult {
  const text = raw.trim()

  if (text === '') {
    return { ok: false, reason: 'Klistra in koden först.' }
  }

  const legacy = text.startsWith(LEGACY_PREFIX)

  if (!legacy && !text.startsWith(PREFIX)) {
    return {
      ok: false,
      reason: 'Det där ser inte ut som en kod från appen. En kod börjar med KARRA2.',
    }
  }

  let payload: unknown

  try {
    payload = JSON.parse(fromBase64(text.slice(legacy ? LEGACY_PREFIX.length : PREFIX.length)))
  } catch {
    return {
      ok: false,
      reason: 'Koden gick inte att läsa. Kopiera hela koden — den kan ha blivit avhuggen.',
    }
  }

  const card = legacy ? fromLegacy(payload) : fromCurrent(payload)

  return card === null
    ? { ok: false, reason: 'Koden gick att läsa men innehöll inget spelarkort.' }
    : { ok: true, card, legacy }
}

function fromCurrent(payload: unknown): PlayerCardData | null {
  if (payload === null || typeof payload !== 'object') {
    return null
  }

  const candidate = payload as Partial<PlayerCardData>

  if (!Array.isArray(candidate.children) || !Array.isArray(candidate.reports)) {
    return null
  }

  /*
   * Koden bar sin egen version och kan vara aldre an telefonen som laser den -- en kod
   * sparad forra sasongen ar precis det som ska ga att lasa. Den maste darfor genom samma
   * migreringskedja som lagringen, annars skrivs den ner som aktuell utan att vara det.
   */
  return migrate({
    version: typeof candidate.version === 'number' ? candidate.version : CURRENT_VERSION,
    children: candidate.children,
    reports: candidate.reports,
    lastBackupUtc: candidate.lastBackupUtc ?? null,
  })
}

/** Föregångarens form. Avläst ur `index 4.html`, inte antagen. */
interface LegacyBackup {
  kids?: { id?: unknown; name?: unknown; team?: unknown }[]
  stats?: Record<
    string,
    {
      us?: unknown
      them?: unknown
      kids?: Record<string, { g?: unknown; a?: unknown }>
    }
  >
}

/**
 * Översätter den gamla koden.
 *
 * <para>
 * Statistikens nyckel är <c>datum_lag_motståndare</c>. Datumet går att rädda ur den —
 * matchens id gör det inte, eftersom den gamla appen inte hade några. Rapporterna kommer
 * därför in utan koppling till en match i listan, men <b>med sin statistik i behåll</b>.
 * Att slänga dem för att kopplingen saknas vore att kasta bort just det familjen ville
 * flytta med sig.
 * </para>
 */
function fromLegacy(payload: unknown): PlayerCardData | null {
  if (payload === null || typeof payload !== 'object') {
    return null
  }

  const legacy = payload as LegacyBackup

  if (!Array.isArray(legacy.kids)) {
    return null
  }

  const card = emptyCard()

  card.children = legacy.kids
    .filter((kid) => typeof kid.id === 'string' && typeof kid.name === 'string')
    .map((kid) => ({
      id: kid.id as string,
      name: kid.name as string,
      shirtNumber: null,
      teamSlug: typeof kid.team === 'string' ? kid.team : null,
      seenBadges: [],
    }))

  for (const [key, entry] of Object.entries(legacy.stats ?? {})) {
    const parts = key.split('_')
    const date = parts[0] ?? ''

    /*
     * Nyckeln ar `datum_lag_motstandare`, sa motstandaren gar att rada ur den. Resten av
     * nyckeln fogas ihop igen ifall ett lagnamn skulle innehalla ett understreck -- en
     * avhuggen motstandare vore samre an ingen alls.
     */
    const opponent = parts.slice(2).join('_')
    const playedUtc = /^\d{4}-\d{2}-\d{2}$/.test(date)
      ? `${date}T12:00:00.000Z`
      : new Date(0).toISOString()

    for (const [childId, values] of Object.entries(entry.kids ?? {})) {
      card.reports.push({
        id: `${key}-${childId}`,
        childId,
        matchId: null,
        playedUtc,
        goals: asCount(values.g),
        assists: asCount(values.a),
        teamGoals: asCountOrNull(entry.us),
        opponentGoals: asCountOrNull(entry.them),
        opponent: opponent === '' ? null : opponent,
        note: null,
      })
    }
  }

  /*
   * En hel sasong kommer in pa en gang. Marken som redan ar fortjanade markeras darfor som
   * sedda -- samma val som migreringen gor, av samma skal: sex firanden i rad firar
   * ingenting som just hant.
   */
  return markEarnedAsSeen(card)
}

function asCount(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? Math.floor(value) : 0
}

function asCountOrNull(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0
    ? Math.floor(value)
    : null
}

/**
 * Base64 med svenska tecken intakta.
 *
 * `btoa` klarar bara latin-1, så ett barn som heter Åsa hade fått koden att kasta. Samma
 * omväg som föregångaren använde, vilket också är varför koderna går att läsa åt båda håll.
 */
function toBase64(text: string): string {
  return btoa(String.fromCharCode(...new TextEncoder().encode(text)))
}

function fromBase64(encoded: string): string {
  return new TextDecoder().decode(
    Uint8Array.from(atob(encoded), (character) => character.charCodeAt(0)),
  )
}
