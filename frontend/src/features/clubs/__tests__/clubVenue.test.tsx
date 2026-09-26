import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { ClubVenueSettings } from '@/features/clubs'
import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderWithProviders } from '@/test/renderWithProviders'

/**
 * Klubbens hemmaplan i inställningarna (`#307`): en tränare skriver in namn + adress, servern
 * geokodar, och hemma-adressen fylls sedan i automatiskt när en aktivitet läggs upp.
 */

interface Sent {
  url: string
  method: string
  body: unknown
}

function stub(current: unknown): Sent[] {
  const sent: Sent[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      sent.push({
        url,
        method,
        body: typeof init?.body === 'string' ? JSON.parse(init.body) : null,
      })

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/club-venue')) {
        if (method === 'PUT') return Promise.resolve(new Response(null, { status: 204 }))
        return Promise.resolve(jsonResponse(current))
      }
      return Promise.resolve(jsonResponse({}))
    }),
  )

  return sent
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  setAccessToken('token')
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('klubbens hemmaplan', () => {
  it('sparar namn och adress — anropet bär dem', async () => {
    const user = userEvent.setup()
    const sent = stub({
      name: null,
      address: null,
      latitude: null,
      longitude: null,
      configured: false,
    })

    await renderWithProviders(<ClubVenueSettings truppId="trupp-1" />)

    await user.type(await screen.findByLabelText('Namn på planen'), 'Klarebergsvallen')
    await user.type(screen.getByLabelText('Adress'), 'Klarebergsvallen, Göteborg')
    await user.click(screen.getByRole('button', { name: 'Spara hemmaplan' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/admin/trupper/trupp-1/club-venue') &&
            r.method === 'PUT' &&
            (r.body as { name: string }).name === 'Klarebergsvallen' &&
            (r.body as { address: string }).address === 'Klarebergsvallen, Göteborg',
        ),
      ).toBe(true)
    })

    expect(await screen.findByText(/Sparat/)).toBeInTheDocument()
  })

  it('förfyller fälten med den redan sparade planen', async () => {
    stub({
      name: 'Karra IP',
      address: 'Idrottsvägen 1, Göteborg',
      latitude: 57.79,
      longitude: 11.94,
      configured: true,
    })

    await renderWithProviders(<ClubVenueSettings truppId="trupp-1" />)

    expect(await screen.findByDisplayValue('Karra IP')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Idrottsvägen 1, Göteborg')).toBeInTheDocument()
    // Knappen säger "ändra" när en plan redan finns.
    expect(screen.getByRole('button', { name: 'Spara ändring' })).toBeInTheDocument()
  })
})
