import { screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

import type { HomeSummary } from '../homeApi'

/**
 * Hem-vyn ("allt samlat").
 *
 * <para>
 * Den ska visa det som är på gång — nästa händelse, kallelser som väntar på svar, senaste i
 * chatten — och lagen som väg in i schemat. Alla tomlägen hanteras, och tiderna kommer ur den
 * enda tidszons-omvandlingen (§KM.5).
 * </para>
 */

const TOKEN = `x.${btoa(JSON.stringify({ email: 'foralder@example.com' }))}.y`

const teams = [{ slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' }]

function stub(summary: HomeSummary) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: TOKEN }))
      }
      if (url.includes('/api/v1/hem')) return Promise.resolve(jsonResponse(summary))

      return Promise.resolve(jsonResponse(teams))
    }),
  )
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  setAccessToken(TOKEN)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('hem-vyn visar det som är på gång', () => {
  it('visar nästa händelse, obesvarad kallelse och senaste i chatten', async () => {
    stub({
      nextEvent: {
        id: 'e1',
        type: 'Match',
        kickoffUtc: '2026-09-20T12:00:00Z',
        title: null,
        opponent: 'Torslanda',
        isHome: true,
        teamSlug: 'gul',
        teamName: 'Gul',
        place: 'Karra IP',
      },
      pendingKallelser: [
        {
          eventId: 'e1',
          type: 'Match',
          kickoffUtc: '2026-09-20T12:00:00Z',
          title: null,
          opponent: 'Torslanda',
          isHome: true,
          teamName: 'Gul',
          unansweredCount: 2,
        },
      ],
      latestChat: {
        truppId: 't1',
        teamSlug: null,
        channelName: 'P2016 chatt',
        authorName: 'Anna Andersson',
        snippet: 'Vi ses imorgon!',
        sentAtUtc: '2026-09-19T18:00:00Z',
      },
    })

    renderRoute('/')

    await screen.findByRole('heading', { name: 'Nästa händelse' })

    // Nästa händelse, med en länk till detaljen. Både denna och kallelsen nedan beskriver samma
    // match, så varje del prövas inom sin egen sektion.
    const nextSection = screen.getByRole('region', { name: 'Nästa händelse' })
    const nextLink = within(nextSection).getByRole('link')
    expect(nextLink).toHaveAttribute('href', '/handelse/e1')
    expect(within(nextLink).getByText('Hemma mot Torslanda')).toBeInTheDocument()
    expect(within(nextLink).getByText(/Karra IP/)).toBeInTheDocument()

    // Obesvarad kallelse.
    const pendingSection = screen.getByRole('region', { name: 'Väntar på ditt svar' })
    expect(within(pendingSection).getByText(/2 barn har inte svarat/)).toBeInTheDocument()

    // Senaste i chatten.
    const chatSection = screen.getByRole('region', { name: 'Senaste i chatten' })
    expect(within(chatSection).getByText('Vi ses imorgon!')).toBeInTheDocument()
    expect(within(chatSection).getByText(/P2016 chatt/)).toBeInTheDocument()
    expect(within(chatSection).getByText('Anna Andersson')).toBeInTheDocument()
  })

  it('visar tomlägen och lagen som väg in i schemat när det inte finns något på gång', async () => {
    stub({ nextEvent: null, pendingKallelser: [], latestChat: null })

    renderRoute('/')

    expect(await screen.findByText('Inga kommande händelser just nu.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Väntar på ditt svar' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Senaste i chatten' })).not.toBeInTheDocument()

    // Lagen finns kvar som väg in i hela schemat.
    expect(screen.getByRole('heading', { name: 'Dina lag' })).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: /Gul/ })).toHaveAttribute('href', '/lag/gul')
  })
})
