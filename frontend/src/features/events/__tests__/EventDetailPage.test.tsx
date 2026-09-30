import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { TeamEvent } from '@/features/events'
import { emptyCard, writeCard } from '@/features/playercard'
import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse, stubApi, testEvent, testTeams } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

beforeEach(() => {
  // Händelsesidan kräver inloggning (§KM.3); en gäst skickas till inloggningen i stället.
  // Att gästen omdirigeras prövas i routing-testet — här är alla inloggade medlemmar.
  setAccessToken('test-token')
  // Spelarkortet bor på enheten; nolla det mellan tester så en kvarlämnad rapport inte läcker.
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
  clearSession()
  localStorage.clear()
})

const MATCH_ID = '11111111-2222-3333-4444-555555555555'

/** Ett par dagar fram, så vädret hamnar inom prognosfönstret oavsett när testet körs. */
const FUTURE_KICKOFF = new Date(Date.now() + 2 * 86_400_000).toISOString()
const FUTURE_HOUR = `${FUTURE_KICKOFF.slice(0, 13)}:00`

function detail(overrides: Partial<TeamEvent> = {}) {
  return {
    team: testTeams[0]!,
    match: testEvent(MATCH_ID, '2026-09-20T12:00:00Z', overrides),
  }
}

describe('Matchdetaljsidan — innehåll', () => {
  it('visar alla fält från API:t', async () => {
    stubApi({ match: detail() })

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(
      await screen.findByRole('heading', { name: /Hemma mot Motstandare/ }),
    ).toBeInTheDocument()
    // Tiden är hjälten (stor och tunn), datumet en eyebrow ovanför — inte längre en
    // "kl."-fogad rad. Båda ska finnas. Avspark i svensk tid: 12:00 UTC är 14:00 i september.
    expect(screen.getByRole('main')).toHaveTextContent('Söndag 20 september')
    expect(screen.getByRole('main')).toHaveTextContent('14:00')
    expect(screen.getByText('Klarebergsvallen')).toBeInTheDocument()
    expect(screen.getByText('Klarebergsvallen, Karra')).toBeInTheDocument()
    expect(screen.getByText('Hemmamatch')).toBeInTheDocument()
  })

  it('visar adminens notis till föräldrarna när den finns (#468)', async () => {
    stubApi({ match: detail({ note: 'Ta med gula tröjan' }) })

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByText('Meddelande')).toBeInTheDocument()
    expect(screen.getByText('Ta med gula tröjan')).toBeInTheDocument()
  })

  it('utelämnar meddelanderaden helt när ingen notis finns (#468)', async () => {
    stubApi({ match: detail({ note: null }) })

    renderRoute(`/handelse/${MATCH_ID}`)

    await screen.findByRole('heading', { name: /Hemma mot Motstandare/ })
    expect(screen.queryByText('Meddelande')).not.toBeInTheDocument()
  })

  it('visar rapportkortet för en trupp-övergripande match även för barn med färg-lag (#474)', async () => {
    // Barnet har valt färg-laget "gul" i spelarkortet.
    writeCard({
      ...emptyCard(),
      children: [{ id: '1', name: 'Liam', shirtNumber: null, teamSlug: 'gul', seenBadges: [] }],
    })

    const truppWideMatchId = '77777777-6666-5555-4444-333333333333'
    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown) => {
        const url = String(input)
        if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
        if (url.includes('/auth/refresh'))
          return Promise.resolve(jsonResponse({ accessToken: 'test-token' }))
        if (url.includes(`/api/v1/events/${truppWideMatchId}`)) {
          return Promise.resolve(
            jsonResponse({
              team: null,
              truppId: 'trupp-1',
              truppName: 'P2016',
              event: {
                id: truppWideMatchId,
                type: 'Match',
                kickoffUtc: '2026-09-20T12:00:00Z',
                title: null,
                opponent: 'Torslanda',
                isHome: true,
                status: 'Scheduled',
                address: 'Klarebergsvallen',
                venue: {
                  name: 'Klarebergsvallen',
                  address: 'Klarebergsvallen',
                  latitude: 57.8,
                  longitude: 12,
                },
                note: null,
              },
            }),
          )
        }
        return Promise.resolve(jsonResponse([]))
      }),
    )

    renderRoute(`/handelse/${truppWideMatchId}`)

    // Matchen är trupp-vid (team null). Förr föll barnet med färg-lag ur filtret och kortet
    // försvann; nu matchar en trupp-vid match alla barns kort (#474).
    expect(await screen.findByRole('heading', { name: 'Efter matchen' })).toBeInTheDocument()
  })

  it('skiljer bortamatch från hemmamatch', async () => {
    stubApi({ match: detail({ isHome: false }) })

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByRole('heading', { name: /Borta mot/ })).toBeInTheDocument()
    expect(screen.getByText('Bortamatch')).toBeInTheDocument()
  })

  it('länkar tillbaka till lagets schema', async () => {
    stubApi({ match: detail() })

    renderRoute(`/handelse/${MATCH_ID}`)

    const back = await screen.findByRole('link', { name: /P2016 Gul/ })
    expect(back).toHaveAttribute('href', '/lag/gul')
  })

  it('renderar en trupp-övergripande händelse (utan lag) utan att krascha (#408)', async () => {
    // En cup skapad "hela truppen" har inget lag → svaret ger team = null. Förr kraschade sidan
    // på team.slug; nu visas truppens namn och sidan står kvar.
    const truppWideId = '99999999-8888-7777-6666-555555555555'
    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown) => {
        const url = String(input)
        if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
        if (url.includes('/auth/refresh'))
          return Promise.resolve(jsonResponse({ accessToken: 'test-token' }))
        if (url.includes(`/events/${truppWideId}/cup`)) {
          return Promise.resolve(
            jsonResponse({
              open: false,
              capacity: null,
              spotsTaken: 0,
              spotsLeft: 0,
              isFull: false,
              signedUp: [],
              mine: [],
              teams: [],
            }),
          )
        }
        if (url.includes(`/api/v1/events/${truppWideId}`)) {
          return Promise.resolve(
            jsonResponse({
              team: null,
              truppId: 'trupp-1',
              truppName: 'P2016',
              event: {
                id: truppWideId,
                type: 'Cup',
                kickoffUtc: '2026-09-20T12:00:00Z',
                title: 'Sommarcup',
                opponent: null,
                isHome: false,
                status: 'Scheduled',
                address: 'Cupvägen 1, Göteborg',
                venue: { name: '', address: 'Cupvägen 1, Göteborg', latitude: 57.8, longitude: 12 },
              },
            }),
          )
        }
        return Promise.resolve(jsonResponse({}))
      }),
    )

    renderRoute(`/handelse/${truppWideId}`)

    // Sidan renderar: rubriken (cupens titel) och truppnamnet i stället för en lag-länk.
    expect(await screen.findByRole('heading', { name: 'Sommarcup' })).toBeInTheDocument()
    expect(screen.getByText(/P2016 · Hela truppen/)).toBeInTheDocument()
    // Ingen lag-länk, och ingen felgräns-fallback.
    expect(screen.queryByRole('link', { name: /Gul/ })).not.toBeInTheDocument()
    expect(screen.queryByText('Något gick fel')).not.toBeInTheDocument()
    // Cup-sektionen finns.
    expect(await screen.findByRole('heading', { name: 'Cup-anmälan' })).toBeInTheDocument()
  })
})

describe('Matchdetaljsidan — status', () => {
  it('säger tydligt att matchen är inställd', async () => {
    // Statusen ändrar allt annat på sidan, så den står först och bärs av text — inte av
    // en färgad ram som inte når fram till alla (WCAG 1.4.1).
    stubApi({ match: detail({ status: 'Cancelled' }) })

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByText(/Händelsen är inställd/)).toBeInTheDocument()
    expect(screen.getByText(/Åk inte till spelplatsen/)).toBeInTheDocument()
  })

  it('varnar för att tiden är den gamla när matchen är framflyttad', async () => {
    stubApi({ match: detail({ status: 'Postponed' }) })

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByText(/Händelsen är framflyttad/)).toBeInTheDocument()
    expect(screen.getByText(/tiden nedan är den som gällde tidigare/)).toBeInTheDocument()
  })

  it('visar ingen statusruta för en match som spelas', async () => {
    stubApi({ match: detail() })

    renderRoute(`/handelse/${MATCH_ID}`)

    await screen.findByText('Hemmamatch')
    expect(screen.queryByText(/inställd/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/framflyttad/i)).not.toBeInTheDocument()
  })
})

describe('Matchdetaljsidan — tillstånd', () => {
  it('säger att matchen inte finns i stället för att visa ett fel', async () => {
    // En gammal kalenderpost från förra säsongen är något normalt.
    stubApi({ match: 'notFound' })

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByRole('alert')).toHaveTextContent('Händelsen finns inte')
  })

  it('erbjuder inget nytt försök när matchen inte finns', async () => {
    // Att försöka igen ger samma 404. Knappen hade bara sett ut som en väg framåt.
    stubApi({ match: 'notFound' })

    renderRoute(`/handelse/${MATCH_ID}`)

    await screen.findByRole('alert')
    expect(screen.queryByRole('button', { name: /Försök igen/ })).not.toBeInTheDocument()
  })

  it('skiljer uteblivet nät från övriga fel och låter användaren försöka igen', async () => {
    stubApi({ match: 'error' })

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByRole('alert')).toHaveTextContent('Ingen anslutning')
    expect(screen.getByRole('button', { name: 'Försök igen' })).toBeInTheDocument()
  })

  it('säger ärligt vid 403 och erbjuder inget nytt försök (#378/#400)', async () => {
    // Ett 403 blir aldrig rätt av att försökas igen; knappen hade bara väckt Render i onödan.
    stubApi({ match: 'forbidden' })

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByRole('alert')).toHaveTextContent('inte behörighet')
    expect(screen.queryByRole('button', { name: /Försök igen/ })).not.toBeInTheDocument()
  })

  it('erbjuder en väg tillbaka även när matchen inte gick att hämta', async () => {
    stubApi({ match: 'notFound' })

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByRole('link', { name: 'Till startsidan' })).toBeInTheDocument()
  })
})

describe('Matchlistan länkar till matchen', () => {
  it('gör hela matchkortet till en länk', async () => {
    stubApi({
      matches: { team: testTeams[0]!, matches: [testEvent(MATCH_ID, '2099-09-20T12:00:00Z')] },
    })

    renderRoute('/lag/gul')

    // Kortet ligger i "nästa match"-kortet; listan är tom eftersom matchen visas där.
    const link = await screen.findByRole('link', { name: 'Visa matchen' })
    expect(link).toHaveAttribute('href', `/handelse/${MATCH_ID}`)
  })
})

describe('Matchdetaljsidan — vägbeskrivning', () => {
  it('erbjuder vägbeskrivning för en match som spelas', async () => {
    stubApi({ match: detail() })

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByRole('link', { name: /Vägbeskrivning/ })).toBeInTheDocument()
  })

  it('döljer vägbeskrivningen för en inställd match', async () => {
    // #21: irrelevanta åtgärder ska döljas. En vägbeskrivning till en inställd match
    // leder någon till en plan där ingen match äger rum.
    stubApi({ match: detail({ status: 'Cancelled' }) })

    renderRoute(`/handelse/${MATCH_ID}`)

    await screen.findByText(/Händelsen är inställd/)
    expect(screen.queryByRole('link', { name: /Vägbeskrivning/ })).not.toBeInTheDocument()
  })

  it('döljer vägbeskrivningen för en framflyttad match', async () => {
    // Utan nytt datum vet vi inte när matchen spelas, bara att det inte är nu.
    stubApi({ match: detail({ status: 'Postponed' }) })

    renderRoute(`/handelse/${MATCH_ID}`)

    await screen.findByText(/Händelsen är framflyttad/)
    expect(screen.queryByRole('link', { name: /Vägbeskrivning/ })).not.toBeInTheDocument()
  })

  it('pekar vägbeskrivningen på matchens adress', async () => {
    stubApi({ match: detail() })

    renderRoute(`/handelse/${MATCH_ID}`)

    const link = await screen.findByRole('link', { name: /Vägbeskrivning/ })
    expect(link.getAttribute('href')).toContain(encodeURIComponent('Klarebergsvallen, Karra'))
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

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByText('17°')).toBeInTheDocument()
    expect(screen.getByText('Lätt duggregn')).toBeInTheDocument()
    expect(screen.getByText(/100% risk för nederbörd/)).toBeInTheDocument()
  })

  it('visar inget väder för en match långt fram i tiden', async () => {
    // Prognosfönstret är 15 dagar. Inget anrop görs alls bortom det.
    stubApi({ match: detail({ kickoffUtc: '2099-09-20T12:00:00Z' }) })

    renderRoute(`/handelse/${MATCH_ID}`)

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

    renderRoute(`/handelse/${MATCH_ID}`)

    expect(await screen.findByText('Hemmamatch')).toBeInTheDocument()
    expect(screen.queryByText(/risk för nederbörd/)).not.toBeInTheDocument()
  })
})
