import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Huvudmenyn (`#148`).
 *
 * <para>
 * Det menyn ska bevisa är enkelt och var länge osant: att appens byggda delar går att nå
 * genom att klicka. Spelarkortet, inloggningen och kontosidan låg i drift i flera veckor
 * utan att någon kunde hitta dem.
 * </para>
 *
 * <para>
 * Här vaktas också vad menyn <b>inte</b> gör. Tränarlänken syns bara för en tränare, men
 * det är bekvämlighet och ingen gräns — auktoriseringen ligger i backend.
 * </para>
 */

/** En token med formen huvud.payload.signatur, där mitten bär anspråken. */
function tokenWith(claims: Record<string, unknown>): string {
  return `x.${btoa(JSON.stringify(claims))}.y`
}

const PARENT = tokenWith({ email: 'foralder@example.com' })
const COACH = tokenWith({ email: 'tranare@example.com', coach: ['gul'] })
const ADMIN = tokenWith({ email: 'admin@example.com', role: 'admin' })

/** Släpper loss en förnyelse som hålls tillbaka. Sätts av stubben. */
let releaseRenewal: (() => void) | null = null

/**
 * Svarar som API:t, med förnyelsen som enda rörliga del.
 *
 * <para>
 * `refresh: 'håller'` svarar först när testet säger till. Det är så den halvsekund ser ut
 * då appen ännu inte vet vem som är inloggad — och löftet <b>måste</b> lösas innan testet
 * är slut: `renewSession` håller en pågående förnyelse i en modulvariabel som bara nollas
 * i sitt `finally`. Ett löfte som aldrig löses låser alltså förnyelsen för resten av filen,
 * och nästa test hade sett en förälder som aldrig blir inloggad.
 * </para>
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

      if (url.includes('/api/v1/matches/')) {
        return Promise.resolve(jsonResponse({ title: 'Matchen finns inte' }, 404))
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
 * Menyn, när den är öppen.
 *
 * Navigeringen bor bakom hamburgaren (`#273`): stängd är drawern `inert` och därmed borta ur
 * både tabbordning och skärmläsarträd, så länkarna går inte att nå förrän man öppnat. Den här
 * hjälparen speglar det en användare gör — trycker på "Meny" — och lämnar tillbaka nav-landmärket.
 */
async function openMenu(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: 'Meny' }))
  return screen.findByRole('navigation', { name: 'Huvudmeny' })
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  releaseRenewal = null
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('menyn når appens delar', () => {
  it('har ett tillgängligt namn när den öppnats', async () => {
    stubApi()
    const user = userEvent.setup()

    renderRoute('/spelarkort')

    expect(await openMenu(user)).toBeInTheDocument()
  })

  it('tar en från startsidan till spelarkortet med ett klick', async () => {
    /*
     * Hela poangen med issuet. Innan menyn fanns gick sidan bara att na genom att skriva
     * adressen, och darfor sag appen ut att besta av enbart matchschemat.
     */
    stubApi()
    const user = userEvent.setup()

    renderRoute('/')

    await user.click(within(await openMenu(user)).getByRole('link', { name: 'Spelarkort' }))

    expect(await screen.findByRole('heading', { name: 'Spelarkortet' })).toBeInTheDocument()
  })

  it('tar en till inloggningen med ett klick', async () => {
    stubApi()
    const user = userEvent.setup()

    renderRoute('/spelarkort')

    await user.click(await within(await openMenu(user)).findByRole('link', { name: 'Logga in' }))

    expect(await screen.findByRole('heading', { name: 'Logga in' })).toBeInTheDocument()
  })
})

describe('menyn visar rätt sak för rätt person', () => {
  it('erbjuder inloggning åt den som är utloggad', async () => {
    stubApi()
    const user = userEvent.setup()

    renderRoute('/spelarkort')

    const nav = await openMenu(user)
    expect(await within(nav).findByRole('link', { name: 'Logga in' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Mitt konto' })).not.toBeInTheDocument()
  })

  it('erbjuder kontosidan och utloggning åt den som är inloggad', async () => {
    signedInAs(PARENT)
    const user = userEvent.setup()

    renderRoute('/spelarkort')

    const nav = await openMenu(user)
    expect(await within(nav).findByRole('link', { name: 'Mitt konto' })).toBeInTheDocument()
    expect(within(nav).getByRole('button', { name: 'Logga ut' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Logga in' })).not.toBeInTheDocument()
  })

  it('visar ingen av dem medan sessionen fortfarande förnyas', async () => {
    /*
     * Utan det har hade "Logga in" blinkat forbi for varje inloggad foralder vid varje
     * start -- det ser ut som att appen glomt bort en.
     */
    // Ingen token i minnet: appen förnyar mot cookien vid start (`#255`), och medan det
    // svaret dröjer (`håller`) är status 'okänd'. Släpps svaret loss bär det PARENT-token.
    stubApi({ refresh: 'håller' })
    const user = userEvent.setup()

    renderRoute('/spelarkort')

    const nav = await openMenu(user)

    expect(within(nav).queryByRole('link', { name: 'Logga in' })).not.toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Mitt konto' })).not.toBeInTheDocument()

    // Och nar svaret val kommer: kontosidan, aldrig inloggningen. Menyn står kvar öppen.
    releaseRenewal?.()

    expect(await within(nav).findByRole('link', { name: 'Mitt konto' })).toBeInTheDocument()
  })

  it('visar tränarlänken bara för en tränare', async () => {
    signedInAs(COACH)
    const user = userEvent.setup()

    renderRoute('/spelarkort')

    const link = await within(await openMenu(user)).findByRole('link', { name: 'Tränare' })

    expect(link).toHaveAttribute('href', '/lag/gul/tranare')
  })

  it('visar ingen tränarlänk för en vanlig förälder', async () => {
    signedInAs(PARENT)
    const user = userEvent.setup()

    renderRoute('/spelarkort')

    const nav = await openMenu(user)
    await within(nav).findByRole('link', { name: 'Mitt konto' })

    expect(within(nav).queryByRole('link', { name: 'Tränare' })).not.toBeInTheDocument()
  })

  it('byter inte innehåll beroende på sida: en admin som inte är tränare får ingen tränarlänk ens på en lagsida', async () => {
    /*
     * `#253`. Tidigare vidgade `isAdmin` tranarlanken sa att en administrator fick den bara
     * pa en lagsida (dar teamInPath fanns) och blev av med den overallt annars -- menyn
     * bytte innehall nar man klickade runt. Nu styr rollen ensam.
     */
    signedInAs(ADMIN)
    const user = userEvent.setup()

    renderRoute('/lag/gul')

    // Vänta in att sessionen förnyats så menyn är i sitt inloggade läge.
    const nav = await openMenu(user)
    await within(nav).findByRole('link', { name: 'Mitt konto' })

    expect(within(nav).queryByRole('link', { name: 'Tränare' })).not.toBeInTheDocument()
  })
})

describe('aktuell sida märks ut', () => {
  it('märker ut spelarkortet', async () => {
    stubApi()
    const user = userEvent.setup()

    renderRoute('/spelarkort')

    const nav = await openMenu(user)

    expect(within(nav).getByRole('link', { name: 'Spelarkort' })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(within(nav).getByRole('link', { name: 'Matcher' })).not.toHaveAttribute('aria-current')
  })

  it('märker ut matcherna även på ett lags schema', async () => {
    /*
     * Matchdelen ar tre adresser -- startsidan, lagets schema och en enskild match. Alla
     * tre hor till samma menypost, annars ser den ut att slappa taget nar man klickar sig
     * in i den.
     */
    setAccessToken(PARENT)
    stubApi()
    const user = userEvent.setup()

    renderRoute('/lag/gul')

    const nav = await openMenu(user)

    await waitFor(() => {
      expect(within(nav).getByRole('link', { name: 'Matcher' })).toHaveAttribute(
        'aria-current',
        'page',
      )
    })
  })

  it('märker inte ut någonting på en adress som inte finns', async () => {
    // En 404-sida ska inte pasta att man star i schemat.
    stubApi()
    const user = userEvent.setup()

    renderRoute('/finns-inte')

    const nav = await openMenu(user)

    for (const link of within(nav).getAllByRole('link')) {
      expect(link).not.toHaveAttribute('aria-current')
    }
  })
})
