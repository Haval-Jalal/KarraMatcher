import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { MatchListSection } from '@/features/matches'
import { stubApi, testMatch, testTeams } from '@/test/apiStub'
import { renderWithProviders } from '@/test/renderWithProviders'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** Bygger ett svar från schemaendpointen. */
function schedule(matches: ReturnType<typeof testMatch>[]) {
  return { team: testTeams[0]!, matches }
}

describe('MatchListSection — kortet och listan tillsammans', () => {
  it('visar matchen i kortet men inte i listan', async () => {
    // Regressionstest: kortet och listans första post visade samma match, så sidan såg
    // ut att räkna fel och föräldern fick läsa samma sak två gånger.
    stubApi({
      matches: schedule([
        testMatch('nasta', '2099-09-20T12:00:00Z'),
        testMatch('darefter', '2099-09-27T12:00:00Z'),
      ]),
    })

    renderWithProviders(<MatchListSection slug="gul" />)

    expect(await screen.findByRole('heading', { name: 'Nästa match' })).toBeInTheDocument()
    expect(screen.getAllByText(/Motstandare nasta/)).toHaveLength(1)
    expect(screen.getByText(/Motstandare darefter/)).toBeInTheDocument()
  })

  it('döljer kortet när säsongen är slut och visar hela listan', async () => {
    // Kriteriet från #20: kortet ska försvinna snyggt. Beslutet bor i sektionen, så det
    // prövas här.
    stubApi({ matches: schedule([testMatch('spelad', '2020-08-15T12:00:00Z')]) })

    renderWithProviders(<MatchListSection slug="gul" />)

    expect(await screen.findByText(/Säsongen är slut/)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Nästa match' })).not.toBeInTheDocument()
  })

  it('visar kortet men inget dubblettfel när det bara finns en match kvar', async () => {
    stubApi({ matches: schedule([testMatch('enda', '2099-09-20T12:00:00Z')]) })

    renderWithProviders(<MatchListSection slug="gul" />)

    expect(await screen.findByRole('heading', { name: 'Nästa match' })).toBeInTheDocument()
    expect(screen.getByText('Inga fler matcher är inlagda.')).toBeInTheDocument()
  })
})

describe('MatchListSection — tillstånd', () => {
  it('skiljer på uteblivet nät och okänt lag', async () => {
    stubApi({ matches: 'notFound' })

    renderWithProviders(<MatchListSection slug="finns-inte" />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Laget finns inte')
  })

  it('säger till när nätet är nere', async () => {
    stubApi({ matches: 'error' })

    renderWithProviders(<MatchListSection slug="gul" />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Ingen anslutning')
  })

  it('säger till när laget saknar matcher', async () => {
    stubApi({ matches: schedule([]) })

    renderWithProviders(<MatchListSection slug="gul" />)

    expect(
      await screen.findByText(/Inga matcher är inlagda för det här laget än/),
    ).toBeInTheDocument()
  })
})
