import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { SELECTED_TEAM_STORAGE_KEY } from '@/features/teams/selectedTeamContext'
import { clearSession, setAccessToken } from '@/lib/session'
import { stubApi, testEvent, testTeams } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

beforeEach(() => {
  localStorage.clear()
  // Innehållet är stängt (§KM.3, `#242`): alla dessa vyer kräver inloggning. Gäst-grinden
  // prövas för sig i "gästgrinden" nedan.
  setAccessToken('test-token')
})

afterEach(() => {
  vi.unstubAllGlobals()
  clearSession()
})

describe('routing', () => {
  it('visar lagväljaren på rotadressen för en ny besökare', async () => {
    stubApi({})

    renderRoute('/')

    expect(await screen.findByText('Välj lag för att se matcherna')).toBeInTheDocument()

    // Länk och inte knapp: att välja lag byter adress, och en kontroll som byter adress
    // ska gå att öppna i ny flik och kopiera.
    expect(await screen.findByRole('link', { name: /Gul/ })).toHaveAttribute('href', '/lag/gul')
  })

  it('skickar en återvändande besökare vidare till sitt lag', async () => {
    // Omdirigeringen sker i beforeLoad, så lagväljaren ska aldrig blinka förbi.
    localStorage.setItem(SELECTED_TEAM_STORAGE_KEY, 'bla')
    stubApi({ matches: { team: testTeams[1]!, matches: [] } })

    const { router } = renderRoute('/')

    expect(await screen.findByRole('heading', { name: 'Schema' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/lag/bla')
  })

  it('visar lagets schema på en delad länk', async () => {
    // Ingen sparad inställning: mottagaren av länken ska ändå landa på rätt lag.
    stubApi({
      matches: { team: testTeams[0]!, matches: [testEvent('a', '2026-09-20T12:00:00Z')] },
    })

    renderRoute('/lag/gul')

    expect(await screen.findByText('P2016 Gul')).toBeInTheDocument()
  })

  it('kommer ihåg laget från en delad länk', async () => {
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    renderRoute('/lag/gul')

    await screen.findByText('P2016 Gul')
    expect(localStorage.getItem(SELECTED_TEAM_STORAGE_KEY)).toBe('gul')
  })

  it('byter lag via väljaren och byter adress', async () => {
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    const user = userEvent.setup()
    const { router } = renderRoute('/lag/gul')

    await user.click(await screen.findByRole('link', { name: /Blå/ }))

    expect(router.state.location.pathname).toBe('/lag/bla')
  })

  it('visar 404-sidan för en adress som inte finns', async () => {
    stubApi({})

    renderRoute('/finns-inte')

    expect(await screen.findByRole('heading', { name: 'Sidan finns inte' })).toBeInTheDocument()
  })
})

describe('gästgrinden', () => {
  it('skickar en gäst till inloggningen med vägen tillbaka i next', async () => {
    // §KM.3, `#242`: allt innehåll kräver inloggning. En gäst på en delad länk möts av
    // inloggningen — och `next` bär adressen med, så hen kommer tillbaka dit efteråt.
    clearSession()
    stubApi({})

    const { router } = renderRoute('/lag/gul')

    expect(await screen.findByRole('heading', { name: 'Logga in' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/logga-in')
    expect(router.state.location.search).toMatchObject({ next: '/lag/gul' })
  })

  it('skickar en gäst från en händelselänk till inloggningen', async () => {
    clearSession()
    stubApi({})

    const { router } = renderRoute('/handelse/abc')

    expect(await screen.findByRole('heading', { name: 'Logga in' })).toBeInTheDocument()
    expect(router.state.location.search).toMatchObject({ next: '/handelse/abc' })
  })

  it('släpper in en inloggad på samma länk', async () => {
    setAccessToken('test-token')
    stubApi({ matches: { team: testTeams[0]!, matches: [] } })

    renderRoute('/lag/gul')

    expect(await screen.findByText('P2016 Gul')).toBeInTheDocument()
  })
})
