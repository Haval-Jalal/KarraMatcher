import { afterEach, describe, expect, it, vi } from 'vitest'

import { getAuthJson, getJson, postJson } from '@/lib/api'
import { clearSession, setAccessToken } from '@/lib/session'
import { emptyResponse, jsonResponse } from '@/test/apiStub'

/**
 * Hur API-klienten läser ett svar.
 *
 * <h3>Varför det här har ett eget test</h3>
 *
 * Ett tomt svar är inte samma sak som ett misslyckat. `POST /auth/request-code` svarar
 * 202 utan kropp, och en klient som ändå försökte tolka kroppen visade "Kunde inte skicka
 * koden just nu" för en förälder som i samma stund fick koden i mejlen. Felet var
 * osynligt i backend-loggarna, eftersom servern gjort precis rätt.
 */

/** Stubbar fetch så att CSRF-hämtningen alltid lyckas och resten är testets sak. */
function stubFetch(responder: () => Response): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) =>
      Promise.resolve(
        String(input).includes('/auth/csrf') ? jsonResponse({ token: 'csrf' }) : responder(),
      ),
    ),
  )
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('tomma svar', () => {
  it('202 utan kropp är ett lyckat anrop', async () => {
    // Just det här svaret ger request-code. Kastar klienten här får användaren ett
    // felmeddelande om en kod som redan ligger i inkorgen.
    stubFetch(() => emptyResponse(202))

    await expect(postJson<void>('/api/v1/auth/request-code', { email: 'a@b.se' })).resolves.toBe(
      undefined,
    )
  })

  it('204 utan kropp är ett lyckat anrop', async () => {
    stubFetch(() => emptyResponse(204))

    await expect(postJson<void>('/api/v1/auth/logout')).resolves.toBe(undefined)
  })
})

describe('svar med kropp', () => {
  it('tolkas som JSON', async () => {
    stubFetch(() => jsonResponse({ accessToken: 'en.token' }))

    await expect(
      postJson<{ accessToken: string }>('/api/v1/auth/verify-code', {}),
    ).resolves.toEqual({ accessToken: 'en.token' })
  })

  it('ett felsvar blir fortfarande ett fel', async () => {
    // Tomma kroppar får inte ha gjort felhanteringen mildare på vägen.
    stubFetch(() => jsonResponse({ title: 'Koden stämmer inte' }, 401))

    await expect(postJson('/api/v1/auth/verify-code', {})).rejects.toThrow('Koden stämmer inte')
  })
})

describe('hämtningar som beror på vem som frågar', () => {
  it('bär access-token, så servern vet vem som läser', async () => {
    /*
     * Regressionsvakt. Samakningens lista ar oppen men svarar olika: en inloggad ser
     * forarens namn och notis och sitt eget erbjudande som sitt. Utan rubriken sag varje
     * inloggad foralder listan som en gast -- utan att nagot sag trasigt ut.
     */
    setAccessToken('en.access.token')

    const fetchMock = vi.fn((_input: unknown, _init?: RequestInit) =>
      Promise.resolve(jsonResponse([])),
    )
    vi.stubGlobal('fetch', fetchMock)

    await getAuthJson('/api/v1/nagot')

    const init = fetchMock.mock.calls[0]?.[1] ?? {}
    const headers = init.headers as Record<string, string>

    expect(headers['Authorization']).toBe('Bearer en.access.token')

    clearSession()
  })

  it('går utan rubrik för en gäst', async () => {
    // Samma anrop tjanar bada. Den lagger bara till det den har.
    clearSession()

    const fetchMock = vi.fn((_input: unknown, _init?: RequestInit) =>
      Promise.resolve(jsonResponse([])),
    )
    vi.stubGlobal('fetch', fetchMock)

    await getAuthJson('/api/v1/nagot')

    const init = fetchMock.mock.calls[0]?.[1] ?? {}

    expect((init.headers as Record<string, string>)['Authorization']).toBeUndefined()
  })

  it('lämnar de publika hämtningarna utan token', async () => {
    /*
     * Lagets schema cachas pa Vercels edge och svarar da utan att vacka Render (§KM.11).
     * En Authorization-rubrik hade gjort svaret omojligt att dela mellan lasare, och
     * kallstarten ar appens dyraste sekunder.
     */
    setAccessToken('en.access.token')

    const fetchMock = vi.fn((_input: unknown, _init?: RequestInit) =>
      Promise.resolve(jsonResponse({})),
    )
    vi.stubGlobal('fetch', fetchMock)

    await getJson('/api/v1/teams/gul/matches')

    const init = fetchMock.mock.calls[0]?.[1] ?? {}

    expect((init.headers as Record<string, string>)['Authorization']).toBeUndefined()

    clearSession()
  })
})
