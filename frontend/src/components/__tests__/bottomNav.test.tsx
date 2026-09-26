import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Botten-navbaren och "Mer"-sidan (navbar-omdesignen).
 *
 * <para>
 * Det navbaren ska bevisa: att appens byggda delar går att nå genom att trycka, utan att öppna
 * någon panel. De mest använda vyerna ligger i raden längst ned; konto, roll-vyer och utloggning
 * bor på "Mer"-sidan. Här vaktas också vad navbaren <b>inte</b> gör — roll-länkar är bekvämlighet,
 * inte en gräns; auktoriseringen ligger i backend (§KM.3).
 * </para>
 */

/** En token med formen huvud.payload.signatur, där mitten bär anspråken. */
function tokenWith(claims: Record<string, unknown>): string {
  return `x.${btoa(JSON.stringify(claims))}.y`
}

const PARENT = tokenWith({ email: 'foralder@example.com' })
const COACH = tokenWith({ email: 'tranare@example.com', coach: ['gul'] })

/** Släpper loss en förnyelse som hålls tillbaka. Sätts av stubben. */
let releaseRenewal: (() => void) | null = null

/**
 * Svarar som API:t, med förnyelsen som enda rörliga del.
 *
 * `refresh: 'håller'` svarar först när testet säger till — så ser den halvsekund ut då appen
 * ännu inte vet vem som är inloggad. Löftet måste lösas innan testet är slut: `renewSession`
 * håller en pågående förnyelse i en modulvariabel som bara nollas i sitt `finally`.
 */
function stubApi(options: { refresh?: 'ok' | 'nej' | 'håller'; token?: string } = {}) {
  const token = options.token ?? PARENT

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))

      if (url.includes('/auth/refresh')) {
        if (options.refresh === 'håller') {
          return new Promise<Response>((resolve) => {
            releaseRenewal = () => {
              resolve(jsonResponse({ accessToken: token }))
            }
          })
        }

        return Promise.resolve(
          options.refresh === 'ok'
            ? jsonResponse({ accessToken: token })
            : jsonResponse({ title: 'Nej' }, 401),
        )
      }

      if (url.includes('/events')) {
        return Promise.resolve(
          jsonResponse({
            team: { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' },
            events: [],
          }),
        )
      }

      return Promise.resolve(
        jsonResponse([{ slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' }]),
      )
    }),
  )
}

/** Loggar in som en återvändande förälder: ledtråd i lagringen och en förnyelse som säger ja. */
function signedInAs(token: string) {
  stubApi({ refresh: 'ok', token })
  setAccessToken(token)
}

/**
 * Botten-navbaren (landmärket "Huvudmeny").
 *
 * `findBy` och inte `getBy`: routern laddar asynkront, så första renderingen är tom en tick —
 * hjälparen väntar in att navbaren finns.
 */
function bottomNav() {
  return screen.findByRole('navigation', { name: 'Huvudmeny' })
}

/** Länklistan på "Mer"-sidan. */
function merNav() {
  return screen.findByRole('navigation', { name: 'Mer i menyn' })
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  releaseRenewal = null
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('botten-navbaren når appens delar', () => {
  it('visar de primära flikarna utan att öppna någon meny', async () => {
    stubApi()

    renderRoute('/spelarkort')

    const nav = await bottomNav()
    expect(within(nav).getByRole('link', { name: /Schema/ })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: /Spelarkort/ })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: /Mer/ })).toBeInTheDocument()
  })

  it('tar en till "Mer"-sidan med ett klick', async () => {
    stubApi()
    const user = userEvent.setup()

    renderRoute('/spelarkort')

    await user.click(within(await bottomNav()).getByRole('link', { name: /Mer/ }))

    expect(await screen.findByRole('heading', { name: 'Mer' })).toBeInTheDocument()
  })

  it('tar en till spelarkortet med ett klick', async () => {
    // Hela poängen: appens delar ska gå att nå genom att trycka, inte bara genom att skriva
    // adressen.
    stubApi()
    const user = userEvent.setup()

    renderRoute('/mer')

    await user.click(within(await bottomNav()).getByRole('link', { name: /Spelarkort/ }))

    expect(await screen.findByRole('heading', { name: 'Spelarkortet' })).toBeInTheDocument()
  })

  it('döljer chatt och cuper för den som inte är inloggad', async () => {
    // Stängd app: en gäst når varken chatt eller cuper (§KM.3). Flikarna ska då inte finnas.
    stubApi()

    renderRoute('/spelarkort')

    const nav = await bottomNav()
    expect(within(nav).queryByRole('link', { name: /Chatt/ })).not.toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: /Cuper/ })).not.toBeInTheDocument()
  })

  it('visar chatt och cuper för den som är inloggad', async () => {
    signedInAs(PARENT)

    renderRoute('/spelarkort')

    const nav = await bottomNav()
    expect(await within(nav).findByRole('link', { name: /Chatt/ })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: /Cuper/ })).toBeInTheDocument()
  })
})

describe('"Mer"-sidan visar rätt sak för rätt person', () => {
  it('erbjuder inloggning åt den som är utloggad', async () => {
    stubApi()

    renderRoute('/mer')

    const nav = await merNav()
    expect(await within(nav).findByRole('link', { name: 'Logga in' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Mitt konto' })).not.toBeInTheDocument()
  })

  it('erbjuder kontosidan och utloggning åt den som är inloggad', async () => {
    signedInAs(PARENT)

    renderRoute('/mer')

    const nav = await merNav()
    expect(await within(nav).findByRole('link', { name: 'Mitt konto' })).toBeInTheDocument()
    expect(within(nav).getByRole('button', { name: 'Logga ut' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Logga in' })).not.toBeInTheDocument()
  })

  it('visar varken inloggning eller konto medan sessionen fortfarande förnyas', async () => {
    // Utan det hade "Logga in" blinkat förbi för varje inloggad förälder vid varje start.
    stubApi({ refresh: 'håller' })

    renderRoute('/mer')

    const nav = await merNav()
    expect(within(nav).queryByRole('link', { name: 'Logga in' })).not.toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Mitt konto' })).not.toBeInTheDocument()

    releaseRenewal?.()

    expect(await within(nav).findByRole('link', { name: 'Mitt konto' })).toBeInTheDocument()
  })

  it('visar "Sköt laget" bara för den som är tränare för ett lag', async () => {
    signedInAs(COACH)

    renderRoute('/mer')

    const link = await within(await merNav()).findByRole('link', { name: 'Sköt laget' })

    expect(link).toHaveAttribute('href', '/lag/gul/tranare')
  })

  it('visar ingen "Sköt laget"-länk för en vanlig förälder', async () => {
    signedInAs(PARENT)

    renderRoute('/mer')

    const nav = await merNav()
    await within(nav).findByRole('link', { name: 'Mitt konto' })

    expect(within(nav).queryByRole('link', { name: 'Sköt laget' })).not.toBeInTheDocument()
  })
})

describe('aktuell flik märks ut', () => {
  it('märker ut spelarkortet', async () => {
    stubApi()

    renderRoute('/spelarkort')

    const nav = await bottomNav()
    expect(within(nav).getByRole('link', { name: /Spelarkort/ })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(within(nav).getByRole('link', { name: /Schema/ })).not.toHaveAttribute('aria-current')
  })

  it('märker ut schemat även på ett lags schema', async () => {
    // Schemat är flera adresser (start, ett lags schema, en händelse); alla hör till samma flik.
    setAccessToken(PARENT)
    stubApi()

    renderRoute('/lag/gul')

    const nav = await bottomNav()
    await waitFor(() => {
      expect(within(nav).getByRole('link', { name: /Schema/ })).toHaveAttribute(
        'aria-current',
        'page',
      )
    })
  })

  it('märker ut "Mer" på kontosidan', async () => {
    // Kontosidan bor under "Mer" — då ska Mer-fliken märkas ut, inte schemat.
    signedInAs(PARENT)

    renderRoute('/konto')

    const nav = await bottomNav()
    await waitFor(() => {
      expect(within(nav).getByRole('link', { name: /Mer/ })).toHaveAttribute('aria-current', 'page')
    })
  })

  it('märker inte ut någon flik på en adress som inte finns', async () => {
    stubApi()

    renderRoute('/finns-inte')

    const nav = await bottomNav()
    for (const link of within(nav).getAllByRole('link')) {
      expect(link).not.toHaveAttribute('aria-current')
    }
  })
})
