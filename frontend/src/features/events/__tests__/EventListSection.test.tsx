import { screen } from '@testing-library/react'
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

describe('EventListSection — kalenderprenumeration', () => {
  it('erbjuder prenumeration på lagets kalender', async () => {
    stubApi({ matches: schedule([testEvent('a', '2099-09-20T12:00:00Z')]) })

    await renderWithProviders(<EventListSection slug="gul" />)

    expect(await screen.findByRole('link', { name: /Prenumerera i kalendern/ })).toBeInTheDocument()
  })

  it('använder webcal så telefonen erbjuder en prenumeration, inte en nedladdning', async () => {
    // Skillnaden är hela poängen: en nedladdad fil uppdateras aldrig, medan en
    // prenumeration hämtar om av sig själv när en match flyttas.
    stubApi({ matches: schedule([]) })

    await renderWithProviders(<EventListSection slug="gul" />)

    const link = await screen.findByRole('link', { name: /Prenumerera i kalendern/ })
    expect(link.getAttribute('href')).toMatch(/^webcal:\/\/.*\/calendar\/gul\.ics$/)
  })

  it('erbjuder prenumeration även för ett lag utan matcher', async () => {
    // En nystartad säsong har inga matcher än. Föräldern ska kunna prenumerera nu och
    // slippa komma ihåg att göra det senare.
    stubApi({ matches: schedule([]) })

    await renderWithProviders(<EventListSection slug="gul" />)

    expect(await screen.findByRole('link', { name: /Prenumerera i kalendern/ })).toBeInTheDocument()
  })
})
