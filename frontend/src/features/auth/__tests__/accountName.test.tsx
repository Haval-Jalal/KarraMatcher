import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { emptyResponse, jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Namnet på kontot (`#154`).
 *
 * <para>
 * Två saker vaktas. Att den som saknar namn blir tillfrågad — annars fylls det aldrig i,
 * och samåkningen förblir anonym. Och att den som redan har ett inte blir det: en
 * återvändande förälder ska möta noll extra steg, inloggningen är redan två ovanpå en
 * länk hen klickade på.
 * </para>
 */

const TOKEN = `x.${btoa('{"email":"foralder@example.com"}')}.y`

function stubApi(options: { profile: unknown; onSave?: (body: unknown) => void }) {
  const sent: { url: string; method: string; body: unknown }[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      const body: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null

      sent.push({ url, method, body })

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: TOKEN }))
      }
      if (url.includes('/auth/request-code')) return Promise.resolve(emptyResponse(202))
      if (url.includes('/auth/verify-code')) {
        return Promise.resolve(jsonResponse({ accessToken: TOKEN }))
      }

      if (url.includes('/auth/profile')) {
        if (method === 'PUT') {
          options.onSave?.(body)

          const saved = body as { firstName: string; lastName: string | null }

          return Promise.resolve(
            jsonResponse({
              firstName: saved.firstName,
              lastName: saved.lastName,
              displayName:
                saved.lastName === null ? saved.firstName : `${saved.firstName} ${saved.lastName}`,
              needsName: false,
            }),
          )
        }

        return Promise.resolve(jsonResponse(options.profile))
      }

      return Promise.resolve(jsonResponse({ teams: [] }))
    }),
  )

  return sent
}

const noName = { firstName: null, lastName: null, displayName: null, needsName: true }
const named = { firstName: 'Anna', lastName: 'Berg', displayName: 'Anna Berg', needsName: false }

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('appen frågar efter namnet en gång', () => {
  it('frågar den som inte fyllt i något, och skickar det till servern', async () => {
    const sent = stubApi({ profile: noName })

    const user = userEvent.setup()
    renderRoute('/logga-in')

    await user.type(await screen.findByLabelText('Mejladress'), 'foralder@example.com')
    await user.click(screen.getByRole('button', { name: 'Skicka kod' }))

    await user.type(await screen.findByLabelText('Kod från mejlet'), '123456')
    await user.click(screen.getByRole('button', { name: 'Logga in' }))

    await user.type(await screen.findByLabelText('Förnamn'), 'Anna')
    await user.type(screen.getByLabelText('Efternamn (valfritt)'), 'Berg')
    await user.click(screen.getByRole('button', { name: 'Spara och fortsätt' }))

    await waitFor(() => {
      const saved = sent.find((call) => call.method === 'PUT' && call.url.includes('/auth/profile'))

      expect(saved?.body).toEqual({ firstName: 'Anna', lastName: 'Berg' })
    })
  })

  it('frågar inte den som redan har ett namn', async () => {
    // En återvändande förälder ska mötas av det hen var på väg till, inte av ett formulär.
    stubApi({ profile: named })

    const user = userEvent.setup()
    renderRoute('/logga-in')

    await user.type(await screen.findByLabelText('Mejladress'), 'foralder@example.com')
    await user.click(screen.getByRole('button', { name: 'Skicka kod' }))

    await user.type(await screen.findByLabelText('Kod från mejlet'), '123456')
    await user.click(screen.getByRole('button', { name: 'Logga in' }))

    await waitFor(() => {
      expect(screen.queryByLabelText('Förnamn')).not.toBeInTheDocument()
    })
  })

  it('går att hoppa över — namnet kan fyllas i senare', async () => {
    /*
     * Ett obligatoriskt formular mellan koden och matchsidan hade gjort inloggningen till
     * tre steg for alla som skyndar sig. Namnet behovs forst nar man samaker.
     */
    stubApi({ profile: noName })

    const user = userEvent.setup()
    renderRoute('/logga-in')

    await user.type(await screen.findByLabelText('Mejladress'), 'foralder@example.com')
    await user.click(screen.getByRole('button', { name: 'Skicka kod' }))

    await user.type(await screen.findByLabelText('Kod från mejlet'), '123456')
    await user.click(screen.getByRole('button', { name: 'Logga in' }))

    await user.click(await screen.findByRole('button', { name: 'Senare' }))

    await waitFor(() => {
      expect(screen.queryByLabelText('Förnamn')).not.toBeInTheDocument()
    })
  })

  it('kräver ett förnamn men inte ett efternamn', async () => {
    stubApi({ profile: noName })
    setAccessToken(TOKEN)

    const user = userEvent.setup()
    renderRoute('/konto')

    await user.click(await screen.findByRole('button', { name: 'Spara namn' }))

    expect(await screen.findByText('Skriv ditt förnamn.')).toBeInTheDocument()
  })
})

describe('namnet går att ändra på kontosidan', () => {
  it('visar det som är ifyllt och sparar en ändring', async () => {
    const sent = stubApi({ profile: named })
    setAccessToken(TOKEN)

    const user = userEvent.setup()
    renderRoute('/konto')

    expect(await screen.findByText('Anna Berg')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Ändra namn' }))
    await user.clear(screen.getByLabelText('Förnamn'))
    await user.type(screen.getByLabelText('Förnamn'), 'Annika')
    await user.click(screen.getByRole('button', { name: 'Spara namn' }))

    await waitFor(() => {
      const saved = sent.find((call) => call.method === 'PUT')

      expect(saved?.body).toMatchObject({ firstName: 'Annika' })
    })
  })

  it('säger vem som ser namnet', async () => {
    // Frågan en förälder faktiskt har innan hen skriver in sitt namn (§KM.3).
    stubApi({ profile: named })
    setAccessToken(TOKEN)

    renderRoute('/konto')

    expect(await screen.findByText(/syns bara för inloggade i laget/)).toBeInTheDocument()
  })
})
