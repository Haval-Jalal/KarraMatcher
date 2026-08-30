import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { MatchDetail } from '@/features/matches'
import { stubApi, testMatch, testTeams } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

afterEach(() => {
  vi.unstubAllGlobals()
})

const MATCH_ID = '11111111-2222-3333-4444-555555555555'

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
