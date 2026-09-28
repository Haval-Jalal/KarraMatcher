import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Adress-förslag i händelseformuläret (`#307`).
 *
 * <para>
 * Prövar wiringen från fält till förslag: en tränare som väljer "annan plats" och börjar skriva
 * ska få förslagen som `datalist`-alternativ. Photon självt prövas i backend — här stubbas
 * endpointen, precis som allt annat nät i FE-testerna.
 * </para>
 */

function coachToken(slug: string): string {
  return `x.${btoa(JSON.stringify({ email: 'tranare@example.com', coach: slug }))}.y`
}

function stubApi(suggestions: string[]) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: coachToken('gul') }))
      }
      if (url.includes('/address-suggestions')) return Promise.resolve(jsonResponse(suggestions))
      if (url.includes('/club-venue')) {
        return Promise.resolve(
          jsonResponse({
            name: 'Kareby IS',
            address: 'Kareby Hed, Kungälv',
            latitude: 57.9,
            longitude: 12.0,
            configured: true,
          }),
        )
      }
      if (url.includes('/events')) {
        return Promise.resolve(
          jsonResponse({
            team: { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' },
            events: [],
            truppId: 'trupp-p2016',
          }),
        )
      }

      return Promise.resolve(jsonResponse({}))
    }),
  )
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

it('visar adressförslag som datalist-alternativ när man skriver en annan plats', async () => {
  const token = coachToken('gul')
  stubApi(['Bortavägen 5, 442 30 Kungälv', 'Bortavägen 12, 442 30 Kungälv'])
  setAccessToken(token)

  const user = userEvent.setup()
  renderRoute('/lag/gul/tranare')

  await user.click(await screen.findByRole('button', { name: 'Lägg till händelse' }))
  await user.click(await screen.findByRole('radio', { name: /Bortamatch/ }))
  await user.type(await screen.findByLabelText('Adress'), 'Bortav')

  await waitFor(() =>
    expect(
      document.querySelector(
        'datalist#adress-forslag option[value="Bortavägen 5, 442 30 Kungälv"]',
      ),
    ).not.toBeNull(),
  )
})
