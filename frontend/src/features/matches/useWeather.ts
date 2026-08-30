import { useQuery } from '@tanstack/react-query'

import {
  forecastUrl,
  hourKeyFor,
  isWithinForecastRange,
  pickHour,
  type Weather,
} from '@/lib/weather'

/**
 * Vädret vid avspark, eller inget alls.
 *
 * Frågan ställs bara när matchen ligger inom prognosfönstret och koordinaterna går att
 * lita på. Utanför det görs inget anrop — `enabled: false` — i stället för att visa något
 * påhittat.
 *
 * Ett misslyckat anrop får aldrig förstöra sidan (kriterium i #22), så inget kastas vidare
 * och komponenten renderar helt enkelt ingenting. Vädret är en bonus; matchtiden är det
 * föräldern kom för.
 */
export function useWeather(kickoffUtc: string, latitude: number, longitude: number) {
  const url = isWithinForecastRange(kickoffUtc)
    ? forecastUrl(latitude, longitude, kickoffUtc)
    : null

  return useQuery({
    queryKey: ['weather', url],
    enabled: url !== null,

    // En halvtimme. Prognosen för en enskild timme ändras inte snabbare än så, och varje
    // sparat anrop är ett anrop mindre till en tjänst vi inte betalar för.
    staleTime: 30 * 60 * 1000,

    // Ett väder som inte går att hämta är inget att envisas om.
    retry: 1,

    queryFn: async ({ signal }): Promise<Weather | null> => {
      if (url === null) {
        return null
      }

      const response = await fetch(url, { signal })

      if (!response.ok) {
        throw new Error(`Open-Meteo svarade ${String(response.status)}`)
      }

      return pickHour(await response.json(), hourKeyFor(kickoffUtc))
    },
  })
}
