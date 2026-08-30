/**
 * Väder vid avspark, från Open-Meteo.
 *
 * "Behöver vi regnkläder?" är den näst vanligaste frågan i föräldragruppen efter "när
 * spelar de?".
 *
 * <h3>Varför webbläsaren anropar direkt</h3>
 *
 * §KM.6 tillåter uttryckligen Open-Meteo i frontenden, och att gå direkt håller Render
 * utanför: en förälder som kollar vädret väcker då inte en sovande backend och äter inte
 * av de 750 instanstimmarna. Det är samma resonemang som edge-cachen i §KM.11 bygger på.
 *
 * <h3>Koordinaterna</h3>
 *
 * De kommer alltid ur matchsvaret, som i sin tur läser dem ur vår egen `Venue`-tabell —
 * aldrig ur en URL-parameter eller något annat anroparen kan styra. Funktionen nedan
 * vägrar bygga en adress av något som inte är ett rimligt koordinatpar, så ett trasigt
 * värde blir inget anrop i stället för ett anrop till fel plats.
 */

const ENDPOINT = 'https://api.open-meteo.com/v1/forecast'

/**
 * Så långt fram Open-Meteo har en prognos värd namnet. Bortom det visar vi inget väder
 * hellre än något påhittat — en förälder som ser "sol" tre veckor i förväg och sedan står
 * i regn litar inte på appen igen.
 */
export const FORECAST_DAYS = 15

export interface Weather {
  temperatureCelsius: number
  precipitationProbability: number
  weatherCode: number
}

/** Sant om matchen ligger inom prognosfönstret. */
export function isWithinForecastRange(kickoffUtc: string, now: Date = new Date()): boolean {
  const kickoff = new Date(kickoffUtc)

  if (Number.isNaN(kickoff.getTime())) {
    return false
  }

  const days = (kickoff.getTime() - now.getTime()) / 86_400_000

  // Matcher som redan spelats får inget väder: prognosen är ointressant i efterhand, och
  // Open-Meteo svarar ändå inte med den utan ett annat anrop.
  return days >= -1 && days <= FORECAST_DAYS
}

/**
 * Nyckeln för matchens timme i Open-Meteos svar, t.ex. `2026-08-30T11:00`.
 *
 * Anropet görs med `timezone=UTC`, så både nyckeln och svaret är i UTC. Att hålla hela
 * kedjan i UTC gör att sommartidsskiftet aldrig behöver tänkas på här — konverteringen
 * till svensk tid sker på ett enda ställe, och det är inte det här (§KM.5).
 */
export function hourKeyFor(kickoffUtc: string): string {
  const kickoff = new Date(kickoffUtc)

  if (Number.isNaN(kickoff.getTime())) {
    throw new TypeError(`Ogiltig tidsstämpel: ${kickoffUtc}`)
  }

  const pad = (value: number) => String(value).padStart(2, '0')

  return (
    `${String(kickoff.getUTCFullYear())}-${pad(kickoff.getUTCMonth() + 1)}-${pad(kickoff.getUTCDate())}` +
    `T${pad(kickoff.getUTCHours())}:00`
  )
}

/**
 * Bygger adressen till prognosen, eller null om koordinaterna inte går att lita på.
 *
 * Null och inte ett anrop med skräp: ett väder för fel plats är sämre än inget väder.
 */
export function forecastUrl(
  latitude: number,
  longitude: number,
  kickoffUtc: string,
): string | null {
  const validLatitude = Number.isFinite(latitude) && latitude >= -90 && latitude <= 90
  const validLongitude = Number.isFinite(longitude) && longitude >= -180 && longitude <= 180

  if (!validLatitude || !validLongitude) {
    return null
  }

  const day = hourKeyFor(kickoffUtc).slice(0, 10)

  const parameters = new URLSearchParams({
    latitude: latitude.toFixed(4),
    longitude: longitude.toFixed(4),
    hourly: 'temperature_2m,precipitation_probability,weather_code',
    timezone: 'UTC',
    start_date: day,
    end_date: day,
  })

  return `${ENDPOINT}?${parameters.toString()}`
}

interface ForecastResponse {
  hourly?: {
    time?: string[]
    temperature_2m?: (number | null)[]
    precipitation_probability?: (number | null)[]
    weather_code?: (number | null)[]
  }
}

/**
 * Plockar ut timmen som matchen börjar. Null om den saknas i svaret — hellre inget väder
 * än fel timmes väder.
 */
export function pickHour(response: unknown, hourKey: string): Weather | null {
  const hourly = (response as ForecastResponse | null)?.hourly

  if (!hourly?.time) {
    return null
  }

  const index = hourly.time.indexOf(hourKey)

  if (index === -1) {
    return null
  }

  const temperature = hourly.temperature_2m?.[index]
  const precipitation = hourly.precipitation_probability?.[index]
  const code = hourly.weather_code?.[index]

  if (
    typeof temperature !== 'number' ||
    typeof precipitation !== 'number' ||
    typeof code !== 'number'
  ) {
    return null
  }

  return {
    temperatureCelsius: temperature,
    precipitationProbability: precipitation,
    weatherCode: code,
  }
}

/**
 * WMO-koderna Open-Meteo använder, på svenska (§KM.9).
 *
 * Texten är det som når en skärmläsare, så den måste stå för sig själv utan symbolen
 * bredvid — "☀️" säger ingenting uppläst.
 */
const WEATHER_TEXT = new Map<number, string>([
  [0, 'Klart'],
  [1, 'Mest klart'],
  [2, 'Halvklart'],
  [3, 'Mulet'],
  [45, 'Dimma'],
  [48, 'Underkyld dimma'],
  [51, 'Lätt duggregn'],
  [53, 'Duggregn'],
  [55, 'Kraftigt duggregn'],
  [56, 'Underkylt duggregn'],
  [57, 'Kraftigt underkylt duggregn'],
  [61, 'Lätt regn'],
  [63, 'Regn'],
  [65, 'Kraftigt regn'],
  [66, 'Underkylt regn'],
  [67, 'Kraftigt underkylt regn'],
  [71, 'Lätt snöfall'],
  [73, 'Snöfall'],
  [75, 'Kraftigt snöfall'],
  [77, 'Snökorn'],
  [80, 'Lätta regnskurar'],
  [81, 'Regnskurar'],
  [82, 'Kraftiga regnskurar'],
  [85, 'Lätta snöbyar'],
  [86, 'Snöbyar'],
  [95, 'Åska'],
  [96, 'Åska med hagel'],
  [99, 'Kraftig åska med hagel'],
])

export function describeWeather(code: number): string {
  return WEATHER_TEXT.get(code) ?? 'Väder okänt'
}
