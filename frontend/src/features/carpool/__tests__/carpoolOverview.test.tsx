import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Tränarens samåkningsöverblick (`#55`).
 *
 * <para>
 * Vyn finns för en enda fråga: får alla skjuts till bortamatchen? Testerna vaktar därför
 * att matchen <em>utan</em> förare syns och säger det i ord, att en väntande förfrågan
 * märks, och att en tränare för ett annat lag inte ens frågar servern.
 * </para>
 */

/** Token med tränarskap för laget testet använder. */
function coachToken(slug: string): string {
  return `x.${btoa(JSON.stringify({ email: 'tranare@example.com', coach: slug }))}.y`
}

const withDriver = {
  matchId: 'm1',
  kickoffUtc: '2026-09-20T11:00:00Z',
  opponent: 'Torslanda',
  isHome: false,
  offers: [],
  pendingRequests: 1,
  seatsOffered: 3,
  seatsTaken: 1,
  seatsLeft: 2,
  needsDriver: false,
}

const withoutDriver = {
  matchId: 'm2',
  kickoffUtc: '2026-09-27T09:00:00Z',
  opponent: 'Backa',
  isHome: false,
  offers: [],
  pendingRequests: 0,
  seatsOffered: 0,
  seatsTaken: 0,
  seatsLeft: 0,
  needsDriver: true,
}

function stubApi(options: { overview?: unknown[] | 'error'; token: string }) {
  const calls: string[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)
      calls.push(url)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: options.token }))
      }

      if (url.includes('/carpool')) {
        if (options.overview === 'error') return Promise.reject(new TypeError('Failed to fetch'))

        return Promise.resolve(jsonResponse(options.overview ?? []))
      }

      if (url.includes('/matches')) {
        return Promise.resolve(
          jsonResponse({
            team: { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' },
            matches: [],
          }),
        )
      }

      return Promise.resolve(jsonResponse({}))
    }),
  )

  return calls
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('överblicken svarar på tränarens fråga', () => {
  it('säger i ord vilken match som saknar förare', async () => {
    /*
     * Karnan i #55. Raden utan forare ar den enda som kraver en handling, och den maste na
     * fram aven till den som inte skiljer rott fran svart (WCAG 1.4.1).
     */
    const token = coachToken('gul')
    stubApi({ overview: [withDriver, withoutDriver], token })
    setAccessToken(token)

    renderRoute('/lag/gul/tranare')

    expect(await screen.findByText('Ingen har erbjudit skjuts än.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Borta mot Backa' })).toBeInTheDocument()
  })

  it('visar platsräkningen och att någon väntar på svar', async () => {
    const token = coachToken('gul')
    stubApi({ overview: [withDriver], token })
    setAccessToken(token)

    renderRoute('/lag/gul/tranare')

    expect(await screen.findByText(/3 platser erbjudna/)).toBeInTheDocument()
    expect(screen.getByText('2 platser kvar')).toBeInTheDocument()
    expect(screen.getByText('1 förfrågan väntar på svar.')).toBeInTheDocument()
  })

  it('säger till när nätet är nere', async () => {
    // Tränaren står ofta vid en plan med dålig täckning. Tomt är inte samma sak som lugnt.
    const token = coachToken('gul')
    stubApi({ overview: 'error', token })
    setAccessToken(token)

    renderRoute('/lag/gul/tranare')

    expect(await screen.findByText(/Ingen anslutning/)).toBeInTheDocument()
  })

  it('frågar inte servern för ett lag man inte sköter', async () => {
    /*
     * Servern avgor vad som tillats -- det har sparar bara ett 403 och en vackt Render.
     * Tranaren for Gul som skriver in Blas adress moter texten om att hen inte skoter laget.
     */
    const token = coachToken('gul')
    const calls = stubApi({ overview: [withDriver], token })
    setAccessToken(token)

    renderRoute('/lag/bla/tranare')

    expect(await screen.findByText(/Du sköter inte det här laget/)).toBeInTheDocument()
    expect(calls.some((url) => url.includes('/carpool'))).toBe(false)
  })
})
