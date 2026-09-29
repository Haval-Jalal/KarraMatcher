import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { emptyResponse, jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Tränarens vy (`#36`).
 *
 * <para>
 * Det här görs stående vid en plan, på en telefon. Testerna vaktar de två saker som gör
 * verklig skada om de går fel: att tiden tränaren skriver blir rätt ögonblick, och att
 * "ta bort" inte går att förväxla med "ställ in" — det senare är nästan alltid det rätta,
 * eftersom en inställd match ska bli kvar i schemat, markerad som inställd.
 * </para>
 */

/** Token med tränarskap för det lag testet använder. */
function coachToken(slug: string): string {
  return `x.${btoa(JSON.stringify({ email: 'tranare@example.com', coach: slug }))}.y`
}

/** Token för en trupp-tränare (admin för hela truppen), utan per-lag-anspråk (`#287`). */
function truppCoachToken(truppId: string): string {
  return `x.${btoa(JSON.stringify({ email: 'tranare@example.com', 'admin-trupp': truppId }))}.y`
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

      // Lagtränarens läsvy (`#redesign`) hämtar lagets roster på sidan.
      if (url.includes('/roster')) {
        return Promise.resolve(
          jsonResponse({ team: { id: 't1', name: 'Gul', colorHex: '#D9A21B' }, children: [] }),
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

    await user.click(await screen.findByRole('button', { name: 'Lägg till händelse' }))

    await user.type(await screen.findByLabelText(/Avspark/), '2026-09-20T14:00')
    await user.type(screen.getByLabelText('Motståndare'), 'Torslanda')

    // Hemma är förvalt, så platsen kommer ur klubbens plan — ingen adress att fylla i.
    await user.click(screen.getByRole('button', { name: 'Lägg till händelsen' }))

    await waitFor(() => {
      const created = sent.find((call) => call.method === 'POST' && call.url.endsWith('/events'))

      expect(created?.body).toMatchObject({
        kickoffUtc: '2026-09-20T12:00:00.000Z',
        isHome: true,
        address: null,
      })
    })
  })

  it('lägger upp en träning med rubrik i stället för motståndare (#198)', async () => {
    // Träning och övrigt har ingen motståndare — typväljaren byter ut fälten mot en rubrik,
    // och anropet bär type=Training med opponent null.
    const token = coachToken('gul')
    const sent = stubApi(token)
    setAccessToken(token)

    const user = userEvent.setup()
    renderRoute('/lag/gul/tranare')

    await user.click(await screen.findByRole('button', { name: 'Lägg till händelse' }))
    await user.click(await screen.findByLabelText('Träning'))

    await user.type(await screen.findByLabelText(/Start/), '2026-09-22T18:00')
    await user.type(screen.getByLabelText('Rubrik'), 'Lagträning')

    // En träning har ingen motståndare — men hemma/borta gäller den också (#307).
    expect(screen.queryByLabelText('Motståndare')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Lägg till händelsen' }))

    await waitFor(() => {
      const created = sent.find((call) => call.method === 'POST' && call.url.endsWith('/events'))

      expect(created?.body).toMatchObject({
        type: 'Training',
        title: 'Lagträning',
        opponent: null,
        isHome: true,
      })
    })
  })
})

describe('hemma eller annan plats', () => {
  it('visar klubbens plan när hemma är valt', async () => {
    // Hemma är förvalt — adressen fylls i från klubbens hemmaplan, tränaren skriver inget.
    const token = coachToken('gul')
    stubApi(token)
    setAccessToken(token)

    const user = userEvent.setup()
    renderRoute('/lag/gul/tranare')

    await user.click(await screen.findByRole('button', { name: 'Lägg till händelse' }))

    expect(await screen.findByText(/Kareby Hed, Kungälv/)).toBeInTheDocument()
    // Ingen adress att skriva när platsen kommer ur klubbens plan.
    expect(screen.queryByLabelText('Adress')).not.toBeInTheDocument()
  })

  it('kräver en adress när annan plats är vald', async () => {
    const token = coachToken('gul')
    stubApi(token)
    setAccessToken(token)

    const user = userEvent.setup()
    renderRoute('/lag/gul/tranare')

    await user.click(await screen.findByRole('button', { name: 'Lägg till händelse' }))
    await user.type(await screen.findByLabelText(/Avspark/), '2026-09-20T14:00')
    await user.type(screen.getByLabelText('Motståndare'), 'Torslanda')
    await user.click(screen.getByRole('radio', { name: /Bortamatch/ }))
    await user.click(screen.getByRole('button', { name: 'Lägg till händelsen' }))

    expect(await screen.findByText('Skriv adressen till platsen.')).toBeInTheDocument()
  })

  it('skickar den skrivna adressen för en bortamatch', async () => {
    const token = coachToken('gul')
    const sent = stubApi(token)
    setAccessToken(token)

    const user = userEvent.setup()
    renderRoute('/lag/gul/tranare')

    await user.click(await screen.findByRole('button', { name: 'Lägg till händelse' }))
    await user.type(await screen.findByLabelText(/Avspark/), '2026-09-20T14:00')
    await user.type(screen.getByLabelText('Motståndare'), 'Torslanda')
    await user.click(screen.getByRole('radio', { name: /Bortamatch/ }))
    await user.type(await screen.findByLabelText('Adress'), 'Bortavägen 5, Kungälv')
    await user.click(screen.getByRole('button', { name: 'Lägg till händelsen' }))

    await waitFor(() => {
      const created = sent.find((call) => call.method === 'POST' && call.url.endsWith('/events'))

      expect(created?.body).toMatchObject({ isHome: false, address: 'Bortavägen 5, Kungälv' })
    })
  })
})

describe('fel visas på svenska och pekar på rätt fält', () => {
  it('säger till om motståndaren saknas', async () => {
    const token = coachToken('gul')
    stubApi(token)
    setAccessToken(token)

    const user = userEvent.setup()
    renderRoute('/lag/gul/tranare')

    await user.click(await screen.findByRole('button', { name: 'Lägg till händelse' }))
    await user.click(screen.getByRole('button', { name: 'Lägg till händelsen' }))

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

describe('fel vid ändring visas i stället för att sväljas (#393)', () => {
  const MATCH = {
    id: 'ev1',
    type: 'Match' as const,
    kickoffUtc: '2026-09-20T12:00:00.000Z',
    title: null,
    opponent: 'Torslanda',
    isHome: true,
    status: 'Scheduled' as const,
    address: 'Kareby Hed, Kungälv',
    venue: {
      name: 'Kareby IS',
      address: 'Kareby Hed, Kungälv',
      latitude: 57.9,
      longitude: 12.0,
    },
  }

  /** Som stubApi, men med en seedad match och styrbara fel på ställ-in/ta-bort. */
  function seedStub(token: string, options: { failCancel?: boolean; failDelete?: boolean }) {
    const sent: { url: string; method: string; body: unknown }[] = []

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
        if (url.includes('/auth/refresh'))
          return Promise.resolve(jsonResponse({ accessToken: token }))
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
        if (url.includes('/roster')) {
          return Promise.resolve(
            jsonResponse({ team: { id: 't1', name: 'Gul', colorHex: '#D9A21B' }, children: [] }),
          )
        }
        if (url.includes('/events/ev1/cancel') && method === 'POST') {
          return options.failCancel
            ? Promise.resolve(
                jsonResponse({ title: 'Kunde inte ställa in', detail: 'Redan borttagen.' }, 409),
              )
            : Promise.resolve(jsonResponse({ ...MATCH, status: 'Cancelled' }))
        }
        if (url.includes('/events/ev1') && method === 'DELETE') {
          return options.failDelete
            ? Promise.resolve(
                jsonResponse({ title: 'Kunde inte ta bort', detail: 'Redan borttagen.' }, 409),
              )
            : Promise.resolve(emptyResponse(204))
        }
        if (url.includes('/events')) {
          return Promise.resolve(
            jsonResponse({
              team: { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' },
              events: [MATCH],
              truppId: 'trupp-p2016',
            }),
          )
        }

        return Promise.resolve(jsonResponse({}))
      }),
    )

    return sent
  }

  it('ett misslyckat borttag visar ett fel och stänger inte panelen', async () => {
    const token = coachToken('gul')
    seedStub(token, { failDelete: true })
    setAccessToken(token)

    const user = userEvent.setup()
    renderRoute('/lag/gul/tranare')

    await user.click(await screen.findByRole('button', { name: 'Ta bort Hemma mot Torslanda' }))
    const heading = await screen.findByRole('heading', { name: 'Ta bort Hemma mot Torslanda?' })
    await user.click(screen.getByRole('button', { name: 'Ja, ta bort' }))

    expect(await screen.findByText('Kunde inte ta bort — Redan borttagen.')).toBeInTheDocument()
    // Panelen står kvar öppen så tränaren kan försöka igen eller avbryta.
    expect(heading).toBeInTheDocument()
  })

  it('en lyckad inställning uppdaterar även händelsesidans cache', async () => {
    const token = coachToken('gul')
    seedStub(token, {})
    setAccessToken(token)

    const user = userEvent.setup()
    const { queryClient } = renderRoute('/lag/gul/tranare')
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await user.click(await screen.findByRole('button', { name: 'Ställ in Hemma mot Torslanda' }))
    await user.click(await screen.findByRole('button', { name: 'Ställ in' }))

    // Utan detta behåller en öppen händelsesida gammal tid/inställt-läge (#393).
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: ['event', 'ev1'] }))
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

  it('släpper in en trupp-tränare för ett färg-lag hen inte bär per-lag-anspråk för (#287)', async () => {
    /*
     * En tränare gäller hela truppen, så trupp-tränaren når vilket som helst av truppens
     * färg-lag -- här igenkänt på att lagets truppId (ur schemat) finns i admin-trupp-anspråket,
     * trots att token saknar coach-anspråk för "gul". Servern är grinden; det här är UX:en.
     */
    const token = truppCoachToken('trupp-p2016')
    stubApi(token)
    setAccessToken(token)

    renderRoute('/lag/gul/tranare')

    expect(await screen.findByRole('button', { name: 'Lägg till händelse' })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
