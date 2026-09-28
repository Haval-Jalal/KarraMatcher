import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { jsonResponse } from '@/test/apiStub'
import { renderWithProviders } from '@/test/renderWithProviders'

import { TeamRosterSection } from '../TeamRosterSection'

/**
 * Lagtränarens läsvy av sitt lag (`#redesign`, §KM.3): barnen och deras vårdnadshavares mejl.
 * Ett barn visas som "Liam J" — aldrig hela efternamnet (§KM.1).
 */

afterEach(() => {
  vi.unstubAllGlobals()
})

function stubRoster(body: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)
      if (url.includes('/api/v1/teams/gul/roster')) return Promise.resolve(jsonResponse(body))
      return Promise.resolve(jsonResponse({}, 404))
    }),
  )
}

describe('TeamRosterSection', () => {
  it('visar lagets barn och vårdnadshavarnas kontakt', async () => {
    stubRoster({
      team: { id: 't1', name: 'Gul', colorHex: '#D9A21B' },
      children: [
        {
          id: 'c1',
          firstName: 'Liam',
          lastInitial: 'J',
          displayName: 'Liam J',
          teamId: 't1',
          teamName: 'Gul',
          guardians: [{ accountId: 'a1', displayName: 'Anna A', email: 'anna@example.com' }],
        },
      ],
    })

    await renderWithProviders(<TeamRosterSection slug="gul" />)

    expect(await screen.findByText('Liam J')).toBeInTheDocument()
    expect(screen.getByText('Anna A')).toBeInTheDocument()
    const mail = screen.getByRole('link', { name: 'anna@example.com' })
    expect(mail).toHaveAttribute('href', 'mailto:anna@example.com')
  })

  it('visar ett tomläge när laget saknar barn', async () => {
    stubRoster({ team: { id: 't1', name: 'Gul', colorHex: '#D9A21B' }, children: [] })

    await renderWithProviders(<TeamRosterSection slug="gul" />)

    expect(await screen.findByText(/Inga barn i laget än/)).toBeInTheDocument()
  })
})
