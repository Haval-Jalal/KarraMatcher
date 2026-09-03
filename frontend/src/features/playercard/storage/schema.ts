/**
 * Spelarkortets form på enheten.
 *
 * <h3>Varför det här är den känsligaste filen i frontenden</h3>
 *
 * Spelarkortet finns bara här. Det når aldrig servern (§KM.2), så det finns ingen kopia
 * att hämta tillbaka om formen ändras fel — en migrering som tappar ett fält har tappat
 * det för alltid, hos varje familj som redan använt appen.
 *
 * Därför bär varje sparad blob sin <b>version</b>, och varje ändring av formen måste
 * lägga till ett migreringssteg. Att läsa data utan att veta vilken version den har är
 * att gissa.
 */

/** Nuvarande schemaversion. Höjs varje gång formen ändras. */
export const CURRENT_VERSION = 3

/** Ett barn, så som familjen själv lagt in det. */
export interface Child {
  id: string
  /** Förnamn eller smeknamn — det familjen kallar barnet. */
  name: string
  /** Tröjnummer, om barnet har ett. */
  shirtNumber: string | null
  teamSlug: string | null

  /**
   * Märken barnet redan fått se firas.
   *
   * <para>
   * Sparas för att firandet ska ske <em>en</em> gång — den gång märket låstes upp. Utan
   * den här listan hade varje omstart av appen firat samma sex märken igen, och det som
   * skulle vara en händelse blivit en påminnelse man klickar bort.
   * </para>
   *
   * <para>
   * Märkena själva sparas aldrig. De räknas fram ur statistiken varje gång, så en ändrad
   * matchrapport ger ett ändrat märke — det är bara <b>vad barnet sett</b> som ligger här.
   * Tillkom i version 3.
   * </para>
   */
  seenBadges: string[]
}

/** En ifylld matchrapport. Fylls i av förälder och barn efter matchen. */
export interface MatchReport {
  id: string
  childId: string
  /** Matchens id i den publika listan, när rapporten hör till en känd match. */
  matchId: string | null
  playedUtc: string
  goals: number
  assists: number

  /**
   * Matchens resultat, som familjen antecknat det.
   *
   * <para>
   * Tomt tills någon fyllt i. Tillkom i version 2 — äldre rapporter får <c>null</c> genom
   * migreringen, eftersom vi inte vet vad de slutade och inte ska hitta på det.
   * </para>
   */
  teamGoals: number | null
  opponentGoals: number | null
  /** Barnets egna ord om matchen. Lämnar aldrig telefonen. */
  note: string | null
}

/** Allt appen sparar om spelarkortet. */
export interface PlayerCardData {
  version: number
  children: Child[]
  reports: MatchReport[]
  /** När kortet senast säkerhetskopierades. Driver påminnelsen i `#47`. */
  lastBackupUtc: string | null
}

export function emptyCard(): PlayerCardData {
  return {
    version: CURRENT_VERSION,
    children: [],
    reports: [],
    lastBackupUtc: null,
  }
}
