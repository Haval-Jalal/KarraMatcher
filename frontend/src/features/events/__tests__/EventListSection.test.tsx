import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { EventListSection } from '@/features/events'
import { stubApi, testEvent, testTeams } from '@/test/apiStub'
import { renderWithProviders } from '@/test/renderWithProviders'

afterEach(() => {
  vi.unstubAllGlobals()
})

/** Bygger ett svar från schemaendpointen. */
function schedule(matches: ReturnType<typeof testEvent>[]) {
  return { team: testTeams[0]!, matches }
}

describe('EventListSection — kortet och listan tillsammans', () => {
  it('visar matchen i kortet men inte i listan', async () => {
    // Regressionstest: kortet och listans första post visade samma match, så sidan såg
    // ut att räkna fel och föräldern fick läsa samma sak två gånger.
    stubApi({
      matches: schedule([
        testEvent('nasta', '2099-09-20T12:00:00Z'),
        testEvent('darefter', '2099-09-27T12:00:00Z'),
      ]),
    })

    await renderWithProviders(<EventListSection slug="gul" />)

    expect(await screen.findByRole('heading', { name: 'Nästa match' })).toBeInTheDocument()
    expect(screen.getAllByText(/Motstandare nasta/)).toHaveLength(1)
    expect(screen.getByText(/Motstandare darefter/)).toBeInTheDocument()
  })

  it('filtrerar schemat per händelsetyp (#304)', async () => {
    const user = userEvent.setup()
    stubApi({
      matches: schedule([
        testEvent('match1', '2099-09-20T12:00:00Z'),
        testEvent('trana1', '2099-09-22T18:00:00Z', {
          type: 'Training',
          title: 'Lagträning',
          opponent: null,
          isHome: null,
        }),
      ]),
    })

    await renderWithProviders(<EventListSection slug="gul" />)

    // Under "Alla" syns både matchen och träningen.
    expect(await screen.findByText('Lagträning')).toBeInTheDocument()
    expect(screen.getByText(/Motstandare match1/)).toBeInTheDocument()

    // Filtrera till Träningar → matchen försvinner, träningen är kvar.
    await user.click(screen.getByRole('button', { name: 'Träningar' }))

    expect(screen.getByText('Lagträning')).toBeInTheDocument()
    expect(screen.queryByText(/Motstandare match1/)).not.toBeInTheDocument()
  })

  it('döljer kortet när säsongen är slut och visar hela listan', async () => {
    // Kriteriet från #20: kortet ska försvinna snyggt. Beslutet bor i sektionen, så det
    // prövas här.
    stubApi({ matches: schedule([testEvent('spelad', '2020-08-15T12:00:00Z')]) })

    await renderWithProviders(<EventListSection slug="gul" />)

    expect(await screen.findByText(/Säsongen är slut/)).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Nästa match' })).not.toBeInTheDocument()
  })

  it('visar kortet men inget dubblettfel när det bara finns en match kvar', async () => {
    stubApi({ matches: schedule([testEvent('enda', '2099-09-20T12:00:00Z')]) })

    await renderWithProviders(<EventListSection slug="gul" />)

    expect(await screen.findByRole('heading', { name: 'Nästa match' })).toBeInTheDocument()
    expect(screen.getByText('Inga fler händelser är inlagda.')).toBeInTheDocument()
  })
})

describe('EventListSection — tillstånd', () => {
  it('skiljer på uteblivet nät och okänt lag', async () => {
    stubApi({ matches: 'notFound' })

    await renderWithProviders(<EventListSection slug="finns-inte" />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Laget finns inte')
  })

  it('säger till när nätet är nere', async () => {
    stubApi({ matches: 'error' })

    await renderWithProviders(<EventListSection slug="gul" />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Ingen anslutning')
  })

  it('säger till när laget saknar matcher', async () => {
    stubApi({ matches: schedule([]) })

    await renderWithProviders(<EventListSection slug="gul" />)

    expect(
      await screen.findByText(/Inga händelser är inlagda för det här laget än/),
    ).toBeInTheDocument()
  })
})
