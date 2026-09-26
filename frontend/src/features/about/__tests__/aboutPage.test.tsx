import { screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { emptyResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * "Om appen" (`#605`).
 *
 * <para>
 * Sidan bär förtroendeargumentet — byggd för integritet — och testet vaktar att det faktiskt
 * sägs, att en gäst kan nå sidan utan att logga in, och att den länkar vidare till detaljerna.
 * Neutral copy: konkurrenten namnges aldrig.
 * </para>
 */

afterEach(() => {
  vi.unstubAllGlobals()
})

/** Sidan hämtar ingen data. Auth-lagret försöker ändå läsa sessionen vid start. */
function stubNoSession() {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(emptyResponse(401))),
  )
}

describe('AboutPage', () => {
  it('nås utan inloggning och visar rubriken', async () => {
    stubNoSession()
    renderRoute('/om')

    expect(await screen.findByRole('heading', { level: 1, name: 'Om Truppen' })).toBeInTheDocument()
  })

  it('säger att så lite som möjligt sparas om ett barn och att spelarkortet stannar', async () => {
    stubNoSession()
    renderRoute('/om')

    await screen.findByRole('heading', { level: 1 })

    expect(screen.getByText(/förnamn, efternamnets första bokstav/i)).toBeInTheDocument()
    expect(screen.getByText(/lämnar aldrig telefonen/i)).toBeInTheDocument()
  })

  it('länkar vidare till den fullständiga integritetstexten', async () => {
    stubNoSession()
    renderRoute('/om')

    await screen.findByRole('heading', { level: 1 })

    const links = screen.getAllByRole('link', { name: 'Så hanteras dina uppgifter' })
    expect(links.length).toBeGreaterThan(0)
  })

  it('länkas från foten på varje sida', async () => {
    stubNoSession()
    renderRoute('/')

    await waitFor(() => {
      expect(screen.getByRole('link', { name: 'Om appen' })).toBeInTheDocument()
    })
  })
})
