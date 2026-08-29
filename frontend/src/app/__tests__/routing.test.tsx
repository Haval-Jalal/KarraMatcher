import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { SELECTED_TEAM_STORAGE_KEY } from '@/features/teams/selectedTeamContext'
import { stubApi, testMatch, testTeams } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('routing', () => {
  it('visar lagväljaren på rotadressen för en ny besökare', async () => {
    stubApi({})

    renderRoute('/')

    expect(await screen.findByText('Välj lag för att se matcherna')).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: /Gul/ })).toBeInTheDocument()
  })

  it('skickar en återvändande besökare vidare till sitt lag', async () => {
    // Omdirigeringen sker i beforeLoad, så lagväljaren ska aldrig blinka förbi.
    localStorage.setItem(SELECTED_TEAM_STORAGE_KEY, 'bla')
    stubApi({ matches: { team: testTeams[1]!, matches: [] } })

    const { router } = renderRoute('/')

    expect(await screen.findByRole('heading', { name: 'Matcher' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/lag/bla')
  })

  it('visar lagets schema på en delad länk', async () => {
    // Ingen sparad inställning: mottagaren av länken ska ändå landa på rätt lag.
    stubApi({
      matches: { team: testTeams[0]!, matches: [testMatch('a', '2026-09-20T12:00:00Z')] },
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

    await user.click(await screen.findByRole('button', { name: /Blå/ }))

    expect(router.state.location.pathname).toBe('/lag/bla')
  })

  it('visar 404-sidan för en adress som inte finns', async () => {
    stubApi({})

    renderRoute('/finns-inte')

    expect(await screen.findByRole('heading', { name: 'Sidan finns inte' })).toBeInTheDocument()
  })
})
