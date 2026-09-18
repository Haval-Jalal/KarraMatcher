import { describeWeather } from '@/lib/weather'

import type { TeamEvent } from './types'
import { useWeather } from './useWeather'

/**
 * Vädret vid start, som en rad i händelsens definitionslista.
 *
 * Renderar ingenting när händelsen ligger för långt fram, när anropet misslyckats, eller
 * medan det pågår. En rad som säger "hämtar väder…" och sedan försvinner är mer störande
 * än värdefull — det här är kompletterande information.
 *
 * Strukturen är `dt`/`dd` i en `div` eftersom komponenten sitter inuti en `dl`.
 */
export function EventWeather({ event }: { event: TeamEvent }) {
  const { data } = useWeather(event.kickoffUtc, event.venue.latitude, event.venue.longitude)

  if (!data) {
    return null
  }

  return (
    <div className="detail__row">
      <dt>Väder</dt>
      <dd className="weather">
        <span className="weather__temperature">{Math.round(data.temperatureCelsius)}°</span>
        <span>{describeWeather(data.weatherCode)}</span>
        <span className="weather__rain">{data.precipitationProbability}% risk för nederbörd</span>
      </dd>
    </div>
  )
}
