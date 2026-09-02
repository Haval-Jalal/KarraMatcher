import { requestPersistentStorage } from './persistentStorage'
import { CURRENT_VERSION, emptyCard, type MatchReport, type PlayerCardData } from './schema'

/**
 * Spelarkortets lagring på enheten.
 *
 * <h3>Ingenting härifrån går ut på nätet</h3>
 *
 * Det är hela §KM.2. Modulen importerar med flit inte API-lagret — det finns inget sätt
 * att av misstag skicka ett barns statistik någonstans, eftersom vägen dit inte finns i
 * den här filen. Ett test kontrollerar att den kopplingen aldrig uppstår.
 *
 * <h3>Varför localStorage och inte IndexedDB</h3>
 *
 * En säsong för två barn är några kilobyte. localStorage är synkront, vilket gör att en
 * komponent kan läsa kortet utan laddningstillstånd — och färre tillstånd är färre sätt
 * att visa fel. Det som skyddar mot att lagringen rensas är inte valet av lagring utan
 * <c>persist()</c> och installation på hemskärmen (§KM.2), och de gäller båda.
 */

const STORAGE_KEY = 'karra.spelarkort'

/**
 * Migreringar från en äldre version till nästa.
 *
 * <para>
 * Nyckeln är versionen datan <em>har</em>; funktionen ger nästa version. Kedjan körs tills
 * datan är aktuell, så en familj som inte öppnat appen på två säsonger tas hela vägen —
 * inte bara ett steg.
 * </para>
 */
const migrations: Record<number, (data: PlayerCardData) => PlayerCardData> = {
  /**
   * 1 → 2: matchrapporten fick resultatfält.
   *
   * <para>
   * Befintliga rapporter får <c>null</c>, inte 0. Vi vet inte vad de matcherna slutade,
   * och en nolla hade sett ut som ett svar — en familj som öppnar en gammal match skulle
   * läsa 0–0 som något de skrivit.
   * </para>
   */
  1: (data) => ({
    ...data,
    version: 2,
    reports: data.reports.map((report) => {
      // Datan har version 1 och saknar faltet, aven om typen sager annat -- typen
      // beskriver nuvarande form, inte den som lases fran disk.
      const legacy = report as Partial<MatchReport>

      return {
        ...report,
        teamGoals: legacy.teamGoals ?? null,
        opponentGoals: legacy.opponentGoals ?? null,
      }
    }),
  }),
}

/**
 * Läser kortet.
 *
 * <para>
 * Svarar med ett tomt kort när lagringen är tom, avstängd eller innehåller något som inte
 * går att tolka. <b>Aldrig ett undantag:</b> en trasig blob får inte göra appen omöjlig
 * att öppna — då vore även den friska datan oåtkomlig.
 * </para>
 */
export function readCard(): PlayerCardData {
  let raw: string | null

  try {
    raw = globalThis.localStorage?.getItem(STORAGE_KEY) ?? null
  } catch {
    // Lagringen kan vara avstängd av en policy eller av privat läge.
    return emptyCard()
  }

  if (raw === null) {
    return emptyCard()
  }

  try {
    const parsed: unknown = JSON.parse(raw)

    if (!isCard(parsed)) {
      return emptyCard()
    }

    return migrate(parsed)
  } catch {
    return emptyCard()
  }
}

/**
 * Sparar kortet.
 *
 * <para>
 * Svarar med om det gick. En full lagring är sällsynt men inte omöjlig, och den som fyllt
 * i en matchrapport ska få veta att den inte sparades — inte tro att den gjorde det.
 * </para>
 */
export function writeCard(data: PlayerCardData): boolean {
  try {
    globalThis.localStorage?.setItem(
      STORAGE_KEY,
      JSON.stringify({ ...data, version: CURRENT_VERSION }),
    )

    askForPersistenceOnce(data)

    return true
  } catch {
    return false
  }
}

/** Sant när begäran om beständig lagring redan gjorts i den här fliken. */
let persistenceAsked = false

/**
 * Ber om beständig lagring första gången kortet faktiskt får innehåll.
 *
 * <h3>Varför inte vid start</h3>
 *
 * De allra flesta som öppnar appen vill se en matchtid och rör aldrig spelarkortet. Att
 * fråga dem vore att visa en ruta för något de inte gör — och en fråga man inte förstår
 * besvaras med nej, vilket gör skyddet sämre för dem som sedan börjar använda kortet.
 *
 * Svaret ignoreras med flit. Ett nej ska inte gå ut över något: appen fungerar likadant
 * utan beständig lagring, den är bara mer utsatt (§KM.2).
 */
function askForPersistenceOnce(data: PlayerCardData): void {
  if (persistenceAsked || data.children.length === 0) {
    return
  }

  persistenceAsked = true

  void requestPersistentStorage()
}

/** Tar bort kortet från enheten. Familjens egen radering (§KM.6). */
export function clearCard(): void {
  try {
    globalThis.localStorage?.removeItem(STORAGE_KEY)
  } catch {
    // Går det inte att rensa är det inget appen kan göra åt.
  }
}

/**
 * Kör migreringskedjan tills datan har aktuell version.
 *
 * <para>
 * Saknas ett steg stannar kedjan och datan lämnas som den är, i stället för att tolkas
 * som om den vore aktuell. Att låtsas är hur ett fält försvinner tyst.
 * </para>
 */
function migrate(data: PlayerCardData): PlayerCardData {
  let current = data

  while (current.version < CURRENT_VERSION) {
    const step = migrations[current.version]

    if (step === undefined) {
      return current
    }

    current = step(current)
  }

  return current
}

/**
 * Grov formkontroll.
 *
 * Blir det här fel är alternativet att en komponent kraschar på ett fält som inte finns,
 * vilket för en förälder ser ut som att statistiken är borta.
 */
function isCard(value: unknown): value is PlayerCardData {
  if (value === null || typeof value !== 'object') {
    return false
  }

  const candidate = value as Partial<PlayerCardData>

  return (
    typeof candidate.version === 'number' &&
    Array.isArray(candidate.children) &&
    Array.isArray(candidate.reports)
  )
}
