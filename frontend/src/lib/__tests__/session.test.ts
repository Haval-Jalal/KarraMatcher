import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { postJson, renewSession } from '@/lib/api'
import { clearSession, getAccessToken, hasSessionHint, setAccessToken } from '@/lib/session'

/**
 * Hur klienten håller sessionen (checklistan 3.1 och 3.2).
 *
 * <h3>Det som vaktas hårdast</h3>
 *
 * Att access-token aldrig hamnar i webbläsarens lagring. En token i `localStorage` går att
 * läsa för vilket skript som helst på sidan och blir dessutom kvar på en lånad telefon
 * långt efter att någon slutat använda appen. Regeln är lätt att bryta av bekvämlighet —
 * det är precis därför den har ett test.
 */

/** Svar som ser ut som API:ts, utan att bero på fetch-implementationen. */
function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as unknown as Response
}

beforeEach(() => {
  localStorage.clear()
  sessionStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('access-token lämnar aldrig minnet', () => {
  it('sparas inte i localStorage eller sessionStorage', () => {
    setAccessToken('en.access.token')

    // JSON.stringify och inte Object.values: lagringen är typad som `any` i jsdom, och
    // en spridning av den gör hela uttrycket otypat.
    const stored = JSON.stringify(localStorage) + JSON.stringify(sessionStorage)

    expect(getAccessToken()).toBe('en.access.token')
    expect(stored).not.toContain('en.access.token')
  })

  it('sparar bara att någon loggat in, inte vad', () => {
    // Flaggan är ingen behörighet. Den avgör bara om appen ska försöka förnya vid start,
    // och den som sätter den för hand blir inte inloggad — bara mött av ett 401.
    setAccessToken('en.access.token')

    expect(hasSessionHint()).toBe(true)
    expect(JSON.stringify(localStorage)).not.toContain('en.access.token')
  })

  it('glömmer allt vid utloggning', () => {
    setAccessToken('en.access.token')

    clearSession()

    expect(getAccessToken()).toBeNull()
    expect(hasSessionHint()).toBe(false)
  })
})

describe('förnyelsen är enkelspårig', () => {
  it('gör ett enda anrop även när flera frågar samtidigt', async () => {
    /*
     * Servern roterar refresh-token och tolkar en aterandvand token som en stold -- da
     * atterkallas hela familjen. Tva samtidiga fornyelser fran var egen klient ser
     * likadana ut, sa utan det har hade appen loggat ut anvandaren sjalv.
     */
    const fetchMock = vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) {
        return Promise.resolve(jsonResponse({ token: 'csrf' }))
      }

      return Promise.resolve(jsonResponse({ accessToken: 'ny.token' }))
    })

    vi.stubGlobal('fetch', fetchMock)

    const results = await Promise.all([renewSession(), renewSession(), renewSession()])

    expect(results).toEqual([true, true, true])

    const refreshCalls = fetchMock.mock.calls.filter(([url]) =>
      String(url).includes('/auth/refresh'),
    )

    expect(refreshCalls).toHaveLength(1)
  })

  it('går att förnya igen efteråt', async () => {
    // Enkelspårigheten får inte bli en spärr som sitter kvar.
    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown) =>
        Promise.resolve(
          String(input).includes('/auth/csrf')
            ? jsonResponse({ token: 'csrf' })
            : jsonResponse({ accessToken: 'ny.token' }),
        ),
      ),
    )

    expect(await renewSession()).toBe(true)
    expect(await renewSession()).toBe(true)
  })
})

describe('401 hanteras på ett enda ställe', () => {
  it('förnyar och gör om anropet en gång', async () => {
    setAccessToken('gammal.token')

    let attempts = 0

    const fetchMock = vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: 'ny.token' }))
      }

      attempts++

      return Promise.resolve(
        attempts === 1 ? jsonResponse({ title: 'Utgången' }, 401) : jsonResponse({ ok: true }),
      )
    })

    vi.stubGlobal('fetch', fetchMock)

    const result = await postJson<{ ok: boolean }>('/api/v1/nagot', {})

    expect(result).toEqual({ ok: true })
    expect(attempts).toBe(2)
    expect(getAccessToken()).toBe('ny.token')
  })

  it('ger upp efter ett försök i stället för att loopa', async () => {
    // En server som svarar 401 på allt får inte få klienten att hamra vidare.
    setAccessToken('gammal.token')

    let attempts = 0

    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown) => {
        const url = String(input)

        if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
        if (url.includes('/auth/refresh')) {
          return Promise.resolve(jsonResponse({ accessToken: 'ny.token' }))
        }

        attempts++

        return Promise.resolve(jsonResponse({ title: 'Utgången' }, 401))
      }),
    )

    await expect(postJson('/api/v1/nagot', {})).rejects.toThrow()
    expect(attempts).toBe(2)
  })

  it('försöker inte förnya för den som aldrig loggat in', async () => {
    // En gäst har ingen token. Att ändå anropa förnyelsen hade väckt Render i onödan.
    const fetchMock = vi.fn((input: unknown) =>
      Promise.resolve(
        String(input).includes('/auth/csrf')
          ? jsonResponse({ token: 'csrf' })
          : jsonResponse({ title: 'Nej' }, 401),
      ),
    )

    vi.stubGlobal('fetch', fetchMock)

    await expect(postJson('/api/v1/nagot', {})).rejects.toThrow()

    expect(
      fetchMock.mock.calls.filter(([url]) => String(url).includes('/auth/refresh')),
    ).toHaveLength(0)
  })
})
