import { describe, expect, it } from 'vitest'

import {
  describeWeather,
  forecastUrl,
  hourKeyFor,
  isWithinForecastRange,
  pickHour,
} from '@/lib/weather'

const now = new Date('2026-09-15T09:00:00Z')

describe('isWithinForecastRange', () => {
  it.each([
    ['idag', '2026-09-15T16:00:00Z'],
    ['imorgon', '2026-09-16T16:00:00Z'],
    ['om 14 dagar', '2026-09-29T09:00:00Z'],
  ])('ger true för en match %s', (_name, kickoff) => {
    expect(isWithinForecastRange(kickoff, now)).toBe(true)
  })

  it('ger false bortom prognosfönstret', () => {
    // En förälder som ser "sol" tre veckor i förväg och sedan står i regn litar inte på
    // appen igen. Inget väder är bättre än påhittat väder.
    expect(isWithinForecastRange('2026-10-15T16:00:00Z', now)).toBe(false)
  })

  it('ger false för en match som redan spelats', () => {
    expect(isWithinForecastRange('2026-08-15T16:00:00Z', now)).toBe(false)
  })

  it('ger false för en trasig tidsstämpel i stället för att kasta', () => {
    // Vädret får aldrig fälla sidan. Ett obrukbart datum blir inget anrop.
    expect(isWithinForecastRange('inte-en-tid', now)).toBe(false)
  })
})

describe('hourKeyFor', () => {
  it('rundar ner till timmen i UTC', () => {
    // Avspark 11:15 hör till timmen 11:00 i prognosen.
    expect(hourKeyFor('2026-08-30T11:15:00Z')).toBe('2026-08-30T11:00')
  })

  it('använder UTC och inte maskinens tid', () => {
    // Testerna körs i America/Los_Angeles. Räknades nyckeln i lokal tid hade den här
    // matchen hamnat på fel dygn och prognosen på fel timme.
    expect(hourKeyFor('2026-08-30T02:30:00Z')).toBe('2026-08-30T02:00')
  })

  it('nollutfyller månad, dag och timme', () => {
    expect(hourKeyFor('2026-01-05T07:00:00Z')).toBe('2026-01-05T07:00')
  })

  it('kastar på en trasig tidsstämpel', () => {
    expect(() => hourKeyFor('inte-en-tid')).toThrow(TypeError)
  })
})

describe('forecastUrl', () => {
  it('bygger en adress med koordinater och matchens dygn', () => {
    const url = forecastUrl(57.78, 11.99, '2026-08-30T11:15:00Z')

    expect(url).toContain('latitude=57.7800')
    expect(url).toContain('longitude=11.9900')
    expect(url).toContain('start_date=2026-08-30')
    expect(url).toContain('end_date=2026-08-30')
    expect(url).toContain('timezone=UTC')
  })

  it.each([
    ['latitud utanför intervallet', 91, 11.99],
    ['longitud utanför intervallet', 57.78, 181],
    ['NaN', Number.NaN, 11.99],
    ['oändlighet', 57.78, Number.POSITIVE_INFINITY],
  ])('vägrar bygga en adress av %s', (_name, latitude, longitude) => {
    // Ett väder för fel plats är sämre än inget väder. Null betyder att inget anrop görs.
    expect(forecastUrl(latitude, longitude, '2026-08-30T11:15:00Z')).toBeNull()
  })

  it('kodar parametrarna så att adressen inte går sönder', () => {
    const url = forecastUrl(-33.87, 151.21, '2026-08-30T11:15:00Z')

    expect(url).toContain('hourly=temperature_2m%2Cprecipitation_probability%2Cweather_code')
  })
})

describe('pickHour', () => {
  const response = {
    hourly: {
      time: ['2026-08-30T10:00', '2026-08-30T11:00', '2026-08-30T12:00'],
      temperature_2m: [16.1, 17.3, 18.0],
      precipitation_probability: [40, 100, 60],
      weather_code: [3, 51, 61],
    },
  }

  it('plockar ut rätt timme', () => {
    expect(pickHour(response, '2026-08-30T11:00')).toEqual({
      temperatureCelsius: 17.3,
      precipitationProbability: 100,
      weatherCode: 51,
    })
  })

  it('ger null när timmen saknas i svaret', () => {
    // Hellre inget väder än fel timmes väder.
    expect(pickHour(response, '2026-08-30T23:00')).toBeNull()
  })

  it.each([
    ['tomt svar', {}],
    ['null', null],
    ['saknad hourly', { hourly: undefined }],
    ['saknade värden', { hourly: { time: ['2026-08-30T11:00'] } }],
    [
      'null i mätserien',
      {
        hourly: {
          time: ['2026-08-30T11:00'],
          temperature_2m: [null],
          precipitation_probability: [50],
          weather_code: [0],
        },
      },
    ],
  ])('ger null för %s i stället för att kasta', (_name, input) => {
    // Ett oväntat svar från en tjänst vi inte styr får inte fälla matchsidan.
    expect(pickHour(input, '2026-08-30T11:00')).toBeNull()
  })
})

describe('describeWeather', () => {
  it.each([
    [0, 'Klart'],
    [3, 'Mulet'],
    [51, 'Lätt duggregn'],
    [65, 'Kraftigt regn'],
    [75, 'Kraftigt snöfall'],
    [95, 'Åska'],
  ])('beskriver kod %i på svenska', (code, expected) => {
    expect(describeWeather(code)).toBe(expected)
  })

  it('har ett begripligt svar för en okänd kod', () => {
    // WMO-listan kan växa. En tom sträng hade sett ut som ett fel i gränssnittet.
    expect(describeWeather(999)).toBe('Väder okänt')
  })
})
