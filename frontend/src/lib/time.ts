/**
 * Enda stället i frontenden där UTC möter svensk tid (§KM.5).
 *
 * Backend lagrar och skickar allt i UTC. Appen visar allt i `Europe/Stockholm` — aldrig i
 * webbläsarens egen tidszon, eftersom en förälder på semester i Spanien ska se samma
 * avsparkstid som en förälder hemma i Kärra.
 *
 * Säsongen passerar sommartidsskiftet i oktober. En match som visas en timme fel är precis
 * den sortens fel som får folk att sluta lita på appen, så konverteringen sker på ett enda
 * ställe och varje funktion här är testad över skiftet.
 *
 * Inget datumbibliotek: `Intl` finns i alla webbläsare vi bryr oss om och kan tidszoner
 * korrekt. Ett bibliotek hade varit ett beroende till, för något plattformen redan gör.
 */

export const SWEDISH_TIME_ZONE = 'Europe/Stockholm'

/** Datum och tid som de ser ut på en klocka i Sverige. */
interface SwedishParts {
  year: number
  month: number
  day: number
  hour: number
  minute: number
}

const numericParts = new Intl.DateTimeFormat('sv-SE', {
  timeZone: SWEDISH_TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
})

const timeOnly = new Intl.DateTimeFormat('sv-SE', {
  timeZone: SWEDISH_TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
})

const weekdayAndDate = new Intl.DateTimeFormat('sv-SE', {
  timeZone: SWEDISH_TIME_ZONE,
  weekday: 'long',
  day: 'numeric',
  month: 'long',
})

const dayAndMonth = new Intl.DateTimeFormat('sv-SE', {
  timeZone: SWEDISH_TIME_ZONE,
  day: 'numeric',
  month: 'long',
})

const weekdayOnly = new Intl.DateTimeFormat('sv-SE', {
  timeZone: SWEDISH_TIME_ZONE,
  weekday: 'long',
})

const monthAndYear = new Intl.DateTimeFormat('sv-SE', {
  timeZone: SWEDISH_TIME_ZONE,
  month: 'long',
  year: 'numeric',
})

/**
 * Tolkar det backend skickade. Kastar hellre än gissar: en trasig tidsstämpel som tyst blir
 * "1 januari 1970" är värre än ett fel som syns, eftersom den ser rimlig ut i en lista.
 */
function toInstant(value: string | Date): Date {
  const instant = typeof value === 'string' ? new Date(value) : value

  if (Number.isNaN(instant.getTime())) {
    throw new TypeError(`Ogiltig tidsstämpel: ${String(value)}`)
  }

  return instant
}

function partsOf(value: string | Date): SwedishParts {
  const found = numericParts.formatToParts(toInstant(value))
  const read = (type: Intl.DateTimeFormatPartTypes): number => {
    const part = found.find((candidate) => candidate.type === type)
    if (part === undefined) {
      throw new TypeError(`Kunde inte läsa ${type} ur tidsstämpeln.`)
    }
    return Number(part.value)
  }

  return {
    year: read('year'),
    month: read('month'),
    day: read('day'),
    hour: read('hour'),
    minute: read('minute'),
  }
}

/** Versal första bokstav. `Intl` ger svenska månader och veckodagar med gemener. */
function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1)
}

/** Avsparkstiden, t.ex. `14:30`. */
export function formatKickoffTime(value: string | Date): string {
  return timeOnly.format(toInstant(value))
}

/** Veckodag och datum, t.ex. `Lördag 24 oktober`. */
export function formatMatchDate(value: string | Date): string {
  return capitalize(weekdayAndDate.format(toInstant(value)))
}

/** Datum utan veckodag, t.ex. `24 oktober`. */
export function formatDayAndMonth(value: string | Date): string {
  return dayAndMonth.format(toInstant(value))
}

/** Månadsrubrik i matchlistan, t.ex. `Oktober 2026`. */
export function formatMonthHeading(value: string | Date): string {
  return capitalize(monthAndYear.format(toInstant(value)))
}

/**
 * Nyckel för att gruppera matcher per svenskt dygn, t.ex. `2026-10-25`.
 *
 * Grupperingen måste ske i svensk tid och inte i UTC: en match klockan 00:30 svensk tid
 * ligger på föregående dygn i UTC och hade hamnat under fel datumrubrik.
 */
export function swedishDayKey(value: string | Date): string {
  const { year, month, day } = partsOf(value)
  return `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`
}

/**
 * Skillnad i hela svenska dygn mellan två ögonblick. Positivt betyder att `value` ligger
 * framåt i tiden.
 *
 * Räknas på kalenderdatum och inte på antal timmar. Skiftdygnet i oktober är 25 timmar
 * långt, så timmar hade gett fel svar just den helgen — som ligger mitt i säsongen.
 */
export function swedishDayDifference(value: string | Date, reference: string | Date): number {
  const a = partsOf(value)
  const b = partsOf(reference)

  const dayA = Date.UTC(a.year, a.month - 1, a.day)
  const dayB = Date.UTC(b.year, b.month - 1, b.day)

  return Math.round((dayA - dayB) / 86_400_000)
}

/** Var en match ligger i förhållande till i dag — styr sektionerna i matchlistan. */
export type MatchDayPosition = 'past' | 'today' | 'upcoming'

export function matchDayPosition(
  value: string | Date,
  reference: string | Date = new Date(),
): MatchDayPosition {
  const difference = swedishDayDifference(value, reference)

  if (difference < 0) return 'past'
  if (difference === 0) return 'today'
  return 'upcoming'
}

/**
 * Läsbar dagsetikett för "nästa match"-kortet: `Idag`, `Imorgon`, `På lördag`, `Om 12 dagar`.
 *
 * Veckodag används bara inom en vecka — längre fram säger "på lördag" inget om vilken
 * lördag som menas. Bortom det anges avståndet i dagar och inte datumet, eftersom kortet
 * redan visar hela datumet strax intill. Etiketten ska svara på *hur snart*, inte upprepa
 * *när*.
 */
export function relativeDayLabel(
  value: string | Date,
  reference: string | Date = new Date(),
): string {
  const difference = swedishDayDifference(value, reference)

  if (difference === 0) return 'Idag'
  if (difference === 1) return 'Imorgon'
  if (difference === -1) return 'Igår'

  if (difference > 1 && difference <= 6) {
    return `På ${weekdayOnly.format(toInstant(value))}`
  }

  if (difference > 6) {
    return `Om ${String(difference)} dagar`
  }

  return `För ${String(Math.abs(difference))} dagar sedan`
}
