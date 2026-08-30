import { describeWeather } from '@/lib/weather'

import type { Match } from './types'
import { useWeather } from './useWeather'

/**
 * Vädret vid avspark, som en rad i matchens definitionslista.
 *
 * Renderar ingenting när matchen ligger för långt fram, när anropet misslyckats, eller
 * medan det pågår. En rad som säger "hämtar väder…" och sedan försvinner är mer störande
 * än värdefull — det här är kompletterande information, inte det sidan handlar om.
 *
 * Strukturen är `dt`/`dd` i en `div` eftersom komponenten sitter inuti en `dl`. En `p`
 * där hade varit ogiltig HTML, och ogiltig HTML är det som gör att skärmläsare börjar
 * gissa.
 */
export function MatchWeather({ match }: { match: Match }) {
  const { data } = useWeather(match.kickoffUtc, match.venue.latitude, match.venue.longitude)

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
