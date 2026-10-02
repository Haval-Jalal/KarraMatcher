import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

import type { HomeSummary } from '../homeApi'

/**
 * Hem-vyn ("allt samlat").
 *
 * <para>
 * Den ska visa det som är på gång — nästa händelse och kallelser som väntar på svar — och lagen
 * som väg in i schemat. "Senaste i chatten" utgick i `#redesign` (chatten når man via egen flik).
 * Alla tomlägen hanteras, och tiderna kommer ur den enda tidszons-omvandlingen (§KM.5).
 * </para>
 */

const TOKEN = `x.${btoa(JSON.stringify({ email: 'foralder@example.com' }))}.y`

const teams = [{ slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' }]

function stub(summary: HomeSummary, options: { consented?: boolean } = {}) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: TOKEN }))
      }
      if (url.includes('/api/v1/hem')) return Promise.resolve(jsonResponse(summary))
      if (url.includes('/consent/me')) {
        return Promise.resolve(
          jsonResponse({
            hasConsentedToCurrent: options.consented ?? true,
            version: '1',
            grantedUtc: null,
            text: null,
          }),
        )
      }

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
  it('visar nästa händelse och obesvarad kallelse', async () => {
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
      latestChat: null,
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

    // "Senaste i chatten" är borttagen (`#redesign`) — chatten når man via egen flik.
    expect(screen.queryByRole('region', { name: 'Senaste i chatten' })).not.toBeInTheDocument()
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

  it('nudgar om samtycke när föräldern inte godkänt, och går att avfärda (#597)', async () => {
    const user = userEvent.setup()
    stub({ nextEvent: null, pendingKallelser: [], latestChat: null }, { consented: false })

    renderRoute('/')

    const nudge = await screen.findByText(/Innan ditt barn kan läggas till i truppen/)
    expect(nudge).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Till samtycke' })).toHaveAttribute('href', '/konto')

    await user.click(screen.getByRole('button', { name: 'Inte nu' }))

    expect(screen.queryByText(/Innan ditt barn kan läggas till i truppen/)).not.toBeInTheDocument()
  })

  it('visar ingen samtyckes-nudge när föräldern redan godkänt', async () => {
    stub({ nextEvent: null, pendingKallelser: [], latestChat: null }, { consented: true })

    renderRoute('/')

    await screen.findByRole('heading', { name: 'Dina lag' })
    expect(screen.queryByText(/Innan ditt barn kan läggas till i truppen/)).not.toBeInTheDocument()
  })

  it('visar inte Truppen-ingången för en förälder utan admin-roll', async () => {
    stub({ nextEvent: null, pendingKallelser: [], latestChat: null })

    renderRoute('/')

    await screen.findByRole('heading', { name: 'Dina lag' })
    expect(screen.queryByRole('heading', { name: 'Truppen' })).not.toBeInTheDocument()
  })

  it('visar Truppen-ingången för en trupp-admin, länkad till truppsidan', async () => {
    // Rollen ligger i token-anspråket `admin-trupp` (`#193`). Kortet är rollstyrt i UI:t; servern
    // (AdminOfTrupp) är den riktiga grinden (§KM.3).
    const adminToken = `x.${btoa(JSON.stringify({ email: 'tranare@example.com', 'admin-trupp': 't1' }))}.y`
    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown) => {
        const url = String(input)

        if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
        if (url.includes('/auth/refresh')) {
          return Promise.resolve(jsonResponse({ accessToken: adminToken }))
        }
        if (url.includes('/api/v1/hem')) {
          return Promise.resolve(
            jsonResponse({ nextEvent: null, pendingKallelser: [], latestChat: null }),
          )
        }

        return Promise.resolve(jsonResponse(teams))
      }),
    )
    setAccessToken(adminToken)

    renderRoute('/')

    const truppLink = await screen.findByRole('link', { name: /Hela truppen/ })
    expect(truppLink).toHaveAttribute('href', '/trupp/t1')
  })
})
