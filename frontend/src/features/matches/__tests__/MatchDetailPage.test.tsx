import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { MatchDetail } from '@/features/matches'
import { stubApi, testMatch, testTeams } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

afterEach(() => {
  vi.unstubAllGlobals()
})

const MATCH_ID = '11111111-2222-3333-4444-555555555555'

/** Ett par dagar fram, så vädret hamnar inom prognosfönstret oavsett när testet körs. */
const FUTURE_KICKOFF = new Date(Date.now() + 2 * 86_400_000).toISOString()
const FUTURE_HOUR = `${FUTURE_KICKOFF.slice(0, 13)}:00`

function detail(overrides: Partial<MatchDetail['match']> = {}): MatchDetail {
  return {
    team: testTeams[0]!,
    match: testMatch(MATCH_ID, '2026-09-20T12:00:00Z', overrides),
  }
}

describe('Matchdetaljsidan — innehåll', () => {
  it('visar alla fält från API:t', async () => {
    stubApi({ match: detail() })

    renderRoute(`/match/${MATCH_ID}`)

    expect(
      await screen.findByRole('heading', { name: /Hemma mot Motstandare/ }),
    ).toBeInTheDocument()
    // Datum och tid renderas som flera textnoder, så jämförelsen sker mot hela sidans
    // text. Avspark i svensk tid: 12:00 UTC är 14:00 i september.
    expect(screen.getByRole('main')).toHaveTextContent('Söndag 20 september kl. 14:00')
    expect(screen.getByText('Klarebergsvallen')).toBeInTheDocument()
    expect(screen.getByText('Klarebergsvallen, Karra')).toBeInTheDocument()
    expect(screen.getByText('Hemmamatch')).toBeInTheDocument()
  })

  it('skiljer bortamatch från hemmamatch', async () => {
    stubApi({ match: detail({ isHome: false }) })

    renderRoute(`/match/${MATCH_ID}`)

    expect(await screen.findByRole('heading', { name: /Borta mot/ })).toBeInTheDocument()
    expect(screen.getByText('Bortamatch')).toBeInTheDocument()
  })

  it('länkar tillbaka till lagets schema', async () => {
    stubApi({ match: detail() })

    renderRoute(`/match/${MATCH_ID}`)

    const back = await screen.findByRole('link', { name: /P2016 Gul/ })
    expect(back).toHaveAttribute('href', '/lag/gul')
  })
})

describe('Matchdetaljsidan — status', () => {
  it('säger tydligt att matchen är inställd', async () => {
    // Statusen ändrar allt annat på sidan, så den står först och bärs av text — inte av
    // en färgad ram som inte når fram till alla (WCAG 1.4.1).
    stubApi({ match: detail({ status: 'Cancelled' }) })

    renderRoute(`/match/${MATCH_ID}`)

    expect(await screen.findByText(/Matchen är inställd/)).toBeInTheDocument()
    expect(screen.getByText(/Åk inte till spelplatsen/)).toBeInTheDocument()
  })

  it('varnar för att tiden är den gamla när matchen är framflyttad', async () => {
    stubApi({ match: detail({ status: 'Postponed' }) })

    renderRoute(`/match/${MATCH_ID}`)

    expect(await screen.findByText(/Matchen är framflyttad/)).toBeInTheDocument()
    expect(screen.getByText(/tiden nedan är den som gällde tidigare/)).toBeInTheDocument()
  })

  it('visar ingen statusruta för en match som spelas', async () => {
    stubApi({ match: detail() })

    renderRoute(`/match/${MATCH_ID}`)

    await screen.findByText('Hemmamatch')
    expect(screen.queryByText(/inställd/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/framflyttad/i)).not.toBeInTheDocument()
  })
})

describe('Matchdetaljsidan — tillstånd', () => {
  it('säger att matchen inte finns i stället för att visa ett fel', async () => {
    // En gammal kalenderpost från förra säsongen är något normalt.
    stubApi({ match: 'notFound' })

    renderRoute(`/match/${MATCH_ID}`)

    expect(await screen.findByRole('alert')).toHaveTextContent('Matchen finns inte')
  })

  it('erbjuder inget nytt försök när matchen inte finns', async () => {
    // Att försöka igen ger samma 404. Knappen hade bara sett ut som en väg framåt.
    stubApi({ match: 'notFound' })

    renderRoute(`/match/${MATCH_ID}`)

    await screen.findByRole('alert')
    expect(screen.queryByRole('button', { name: /Försök igen/ })).not.toBeInTheDocument()
  })

  it('skiljer uteblivet nät från övriga fel och låter användaren försöka igen', async () => {
    stubApi({ match: 'error' })

    renderRoute(`/match/${MATCH_ID}`)

    expect(await screen.findByRole('alert')).toHaveTextContent('Ingen anslutning')
    expect(screen.getByRole('button', { name: 'Försök igen' })).toBeInTheDocument()
  })

  it('erbjuder en väg tillbaka även när matchen inte gick att hämta', async () => {
    stubApi({ match: 'notFound' })

    renderRoute(`/match/${MATCH_ID}`)

    expect(await screen.findByRole('link', { name: 'Till startsidan' })).toBeInTheDocument()
  })
})

describe('Matchlistan länkar till matchen', () => {
  it('gör hela matchkortet till en länk', async () => {
    stubApi({
      matches: { team: testTeams[0]!, matches: [testMatch(MATCH_ID, '2099-09-20T12:00:00Z')] },
    })

    renderRoute('/lag/gul')

    // Kortet ligger i "nästa match"-kortet; listan är tom eftersom matchen visas där.
    const link = await screen.findByRole('link', { name: 'Visa matchen' })
    expect(link).toHaveAttribute('href', `/match/${MATCH_ID}`)
  })
})

describe('Matchdetaljsidan — vägbeskrivning', () => {
  it('erbjuder vägbeskrivning för en match som spelas', async () => {
    stubApi({ match: detail() })

    renderRoute(`/match/${MATCH_ID}`)

    expect(await screen.findByRole('link', { name: /Vägbeskrivning/ })).toBeInTheDocument()
  })

  it('döljer vägbeskrivningen för en inställd match', async () => {
    // #21: irrelevanta åtgärder ska döljas. En vägbeskrivning till en inställd match
    // leder någon till en plan där ingen match äger rum.
    stubApi({ match: detail({ status: 'Cancelled' }) })

    renderRoute(`/match/${MATCH_ID}`)

    await screen.findByText(/Matchen är inställd/)
    expect(screen.queryByRole('link', { name: /Vägbeskrivning/ })).not.toBeInTheDocument()
  })

  it('döljer vägbeskrivningen för en framflyttad match', async () => {
    // Utan nytt datum vet vi inte när matchen spelas, bara att det inte är nu.
    stubApi({ match: detail({ status: 'Postponed' }) })

    renderRoute(`/match/${MATCH_ID}`)

    await screen.findByText(/Matchen är framflyttad/)
    expect(screen.queryByRole('link', { name: /Vägbeskrivning/ })).not.toBeInTheDocument()
  })

  it('pekar vägbeskrivningen på matchens adress', async () => {
    stubApi({ match: detail() })

    renderRoute(`/match/${MATCH_ID}`)

    const link = await screen.findByRole('link', { name: /Vägbeskrivning/ })
    expect(link.getAttribute('href')).toContain(encodeURIComponent('Klarebergsvallen, Karra'))
  })
})

describe('Matchdetaljsidan — kalenderfil', () => {
  it('erbjuder nedladdning för en match som spelas', async () => {
    stubApi({ match: detail() })

    renderRoute(`/match/${MATCH_ID}`)

    const link = await screen.findByRole('link', { name: /Lägg till i kalendern/ })
    expect(link).toHaveAttribute('href', `/calendar/match/${MATCH_ID}.ics`)
    expect(link).toHaveAttribute('download')
  })

  it('döljer kalenderknappen för en inställd match', async () => {
    // Samma regel som vägbeskrivningen: en kalenderpost för en match som inte spelas är
    // sämre än ingen post alls — den ligger kvar och påminner om fel sak.
    stubApi({ match: detail({ status: 'Cancelled' }) })

    renderRoute(`/match/${MATCH_ID}`)

    await screen.findByText(/Matchen är inställd/)
    expect(screen.queryByRole('link', { name: /Lägg till i kalendern/ })).not.toBeInTheDocument()
  })
})

describe('Matchdetaljsidan — väder', () => {
  it('visar temperatur, beskrivning och nederbördsrisk', async () => {
    stubApi({ match: detail({ kickoffUtc: FUTURE_KICKOFF }) })
    const inner = globalThis.fetch
    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown, init?: RequestInit) => {
        const url = String(input)

        if (url.includes('open-meteo.com')) {
          return Promise.resolve({
            ok: true,
            status: 200,
            json: () =>
              Promise.resolve({
                hourly: {
                  time: [FUTURE_HOUR],
                  temperature_2m: [17.3],
                  precipitation_probability: [100],
                  weather_code: [51],
                },
              }),
          } as unknown as Response)
        }

        return inner(input as RequestInfo, init)
      }),
    )

    renderRoute(`/match/${MATCH_ID}`)

    expect(await screen.findByText('17°')).toBeInTheDocument()
    expect(screen.getByText('Lätt duggregn')).toBeInTheDocument()
    expect(screen.getByText(/100% risk för nederbörd/)).toBeInTheDocument()
  })

  it('visar inget väder för en match långt fram i tiden', async () => {
    // Prognosfönstret är 15 dagar. Inget anrop görs alls bortom det.
    stubApi({ match: detail({ kickoffUtc: '2099-09-20T12:00:00Z' }) })

    renderRoute(`/match/${MATCH_ID}`)

    await screen.findByText('Hemmamatch')
    expect(screen.queryByText(/risk för nederbörd/)).not.toBeInTheDocument()
  })

  it('förstör inte sidan när väderanropet misslyckas', async () => {
    // Kriterium i #22. Vädret är en bonus; matchtiden är det föräldern kom för.
    stubApi({ match: detail({ kickoffUtc: FUTURE_KICKOFF }) })
    const inner = globalThis.fetch
    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown, init?: RequestInit) => {
        const url = String(input)

        if (url.includes('open-meteo.com')) {
          return Promise.reject(new TypeError('Failed to fetch'))
        }

        return inner(input as RequestInfo, init)
      }),
    )

    renderRoute(`/match/${MATCH_ID}`)

    expect(await screen.findByText('Hemmamatch')).toBeInTheDocument()
    expect(screen.queryByText(/risk för nederbörd/)).not.toBeInTheDocument()
  })
})
