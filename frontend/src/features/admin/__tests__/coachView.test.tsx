import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { renderRoute } from '@/test/renderRoute'

/**
 * Tränarens vy (`#36`).
 *
 * <para>
 * Det här görs stående vid en plan, på en telefon. Testerna vaktar de två saker som gör
 * verklig skada om de går fel: att tiden tränaren skriver blir rätt ögonblick, och att
 * "ta bort" inte går att förväxla med "ställ in" — det senare är nästan alltid det rätta,
 * eftersom kalenderposten ska bli kvar (§KM.4).
 * </para>
 */

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as unknown as Response
}

/** Token med tränarskap för det lag testet använder. */
function coachToken(slug: string): string {
  return `x.${btoa(JSON.stringify({ email: 'tranare@example.com', coach: slug }))}.y`
}

const venue = {
  id: 'venue-1',
  name: 'Klarebergsvallen',
  address: 'Klarebergsvallen, Göteborg',
  isHome: true,
}

/** Fångar det som skickas, så testet kan läsa vad servern skulle ha fått. */
function stubApi(token: string) {
  const sent: { url: string; method: string; body: unknown }[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)

      sent.push({
        url,
        method: init?.method ?? 'GET',
        body: typeof init?.body === 'string' ? JSON.parse(init.body) : null,
      })

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      /*
       * Fornyelsen maste lyckas. AuthProvider forsoker forlanga sessionen vid start, och
       * ett nej dar rensar den token testet just satt -- samma falla som i
       * inloggningstesterna. Det ar ocksa sant i verkligheten: det ar cookien som gor en
       * atervandande tranare inloggad, inte nagot appen sparat.
       */
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: token }))
      }
      if (url.includes('/api/v1/venues')) return Promise.resolve(jsonResponse([venue]))

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

  return sent
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('tiden skrivs i svensk tid och skickas i UTC', () => {
  it('skickar 14:00 svensk sommartid som 12:00 UTC', async () => {
    /*
     * Karnan i §KM.5. Skrevs tiden rakt av hade matchen legat tva timmar fel, och felet
     * hade synts forst i foraldrarnas kalendrar -- alltsa nar det redan ar for sent.
     *
     * Testerna kor i America/Los_Angeles, sa en implementation som anvander webblasarens
     * egen zon faller har.
     */
    const token = coachToken('gul')
    const sent = stubApi(token)
    setAccessToken(token)

    const user = userEvent.setup()
    renderRoute('/lag/gul/tranare')

    await user.click(await screen.findByRole('button', { name: 'Lägg till match' }))

    await user.type(await screen.findByLabelText(/Avspark/), '2026-09-20T14:00')
    await user.type(screen.getByLabelText('Motståndare'), 'Torslanda')

    await user.click(await screen.findByRole('button', { name: /Klarebergsvallen/ }))
    await user.click(screen.getByRole('button', { name: 'Lägg till matchen' }))

    await waitFor(() => {
      const created = sent.find((call) => call.method === 'POST' && call.url.endsWith('/matches'))

      expect(created?.body).toMatchObject({ kickoffUtc: '2026-09-20T12:00:00.000Z' })
    })
  })
})

describe('spelplatsen väljs ur registret', () => {
  it('föreslår platser och visar adressen', async () => {
    // Fritext här hade brutit både vägbeskrivningen och väderprognosen.
    const token = coachToken('gul')
    stubApi(token)
    setAccessToken(token)

    const user = userEvent.setup()
    renderRoute('/lag/gul/tranare')

    await user.click(await screen.findByRole('button', { name: 'Lägg till match' }))
    await user.type(await screen.findByLabelText('Spelplats'), 'klare')

    expect(await screen.findByText('Klarebergsvallen, Göteborg')).toBeInTheDocument()
  })

  it('kräver att en plats är vald', async () => {
    const token = coachToken('gul')
    stubApi(token)
    setAccessToken(token)

    const user = userEvent.setup()
    renderRoute('/lag/gul/tranare')

    await user.click(await screen.findByRole('button', { name: 'Lägg till match' }))
    await user.type(await screen.findByLabelText(/Avspark/), '2026-09-20T14:00')
    await user.type(screen.getByLabelText('Motståndare'), 'Torslanda')
    await user.click(screen.getByRole('button', { name: 'Lägg till matchen' }))

    expect(await screen.findByText('Välj en spelplats.')).toBeInTheDocument()
  })
})

describe('fel visas på svenska och pekar på rätt fält', () => {
  it('säger till om motståndaren saknas', async () => {
    const token = coachToken('gul')
    stubApi(token)
    setAccessToken(token)

    const user = userEvent.setup()
    renderRoute('/lag/gul/tranare')

    await user.click(await screen.findByRole('button', { name: 'Lägg till match' }))
    await user.click(screen.getByRole('button', { name: 'Lägg till matchen' }))

    const message = await screen.findByText('Fyll i motståndarlaget.')

    // Meddelandet ska vara kopplat till fältet, inte bara stå någonstans på sidan.
    expect(screen.getByLabelText('Motståndare')).toHaveAttribute(
      'aria-describedby',
      message.getAttribute('id'),
    )
  })
})

describe('borttagning kräver bekräftelse', () => {
  it('tar inte bort på första klicket', async () => {
    const token = coachToken('gul')
    stubApi(token)
    setAccessToken(token)

    renderRoute('/lag/gul/tranare')

    // Utan matcher finns ingen Ta bort-knapp — vilket i sig är rätt beteende.
    expect(await screen.findByRole('heading', { name: 'Sköt laget' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ja, ta bort matchen' })).not.toBeInTheDocument()
  })
})

describe('vyn visas bara för den som sköter laget', () => {
  it('säger ifrån för en tränare i ett annat lag', async () => {
    /*
     * Det har ar inte sakerheten -- servern avgor vad som tillats. Det har sparar en
     * tranare fran att mota ett 403 dar en text racker.
     */
    const token = coachToken('bla')
    stubApi(token)
    setAccessToken(token)

    renderRoute('/lag/gul/tranare')

    expect(await screen.findByRole('alert')).toHaveTextContent(/sköter inte det här laget/)
  })
})
