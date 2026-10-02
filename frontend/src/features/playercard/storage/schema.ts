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
export const CURRENT_VERSION = 4

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
  /**
   * Motståndaren, som den hette när rapporten fylldes i.
   *
   * <h3>Varför namnet skrivs av i stället för att slås upp</h3>
   *
   * Spelarkortet ska gå att läsa på ett flygplan, på en telefon utan valt lag, och för en
   * familj som importerat sin säsong från den gamla appen. Att hämta motståndaren ur
   * lagets schema hade krävt nät — och hade öppnat en väg ut från just de filer där §KM.2
   * säger att ingen sådan väg ska finnas.
   *
   * <para>
   * Lagnamn är inte personuppgifter; de står redan i lagets schema. Tomt för rapporter som
   * fanns före version 4 och för koder från den gamla appen, som bär datumet men inte
   * motståndaren.
   * </para>
   */
  opponent: string | null
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
  /**
   * Innehålls-signaturen (se {@link backupSignature}) vid den senaste kopian. Låter vyn säga att
   * det finns <em>nya</em> resultat sedan dess, i stället för att visa "Säkerhetskopierad" för
   * alltid efter en enda tidig kopia (#596). Valfri: äldre kort saknar den, och tolkas då som att
   * vi inte vet — ingen falsk "nya resultat"-varning förrän nästa kopia stämplat en signatur.
   */
  lastBackupSignature?: string | null
}

export function emptyCard(): PlayerCardData {
  return {
    version: CURRENT_VERSION,
    children: [],
    reports: [],
    lastBackupUtc: null,
    lastBackupSignature: null,
  }
}

/**
 * En stabil signatur över det <em>säkerhetskopieringsvärda</em> innehållet — barnens kärnfält och
 * matchrapporterna. Märkens "sedd"-status (<c>seenBadges</c>) räknas inte: att titta på ett firande
 * ska inte se ut som nya resultat att kopiera. Används för att upptäcka om kortet ändrats sedan den
 * senaste kopian (#596). Kollision ger på sin höjd en utebliven påminnelse — kopian fungerar ändå.
 */
export function backupSignature(card: PlayerCardData): string {
  const content = JSON.stringify({
    children: card.children.map((child) => ({
      id: child.id,
      name: child.name,
      shirtNumber: child.shirtNumber,
      teamSlug: child.teamSlug,
    })),
    reports: card.reports,
  })

  // FNV-1a: liten, beroendefri och deterministisk. Räcker gott för att skilja två kort-tillstånd åt.
  let hash = 0x811c9dc5
  for (let i = 0; i < content.length; i += 1) {
    hash ^= content.charCodeAt(i)
    hash = Math.imul(hash, 0x01000193)
  }

  return (hash >>> 0).toString(16)
}
