import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { renderRoute } from '@/test/renderRoute'

/**
 * Inloggningsvyn och de skyddade routerna (`#32`).
 *
 * <para>
 * Flödet ska kännas som något man gör en gång per telefon och sedan aldrig tänker på.
 * Det som testas här är därför inte bara att det fungerar, utan att det inte avslöjar
 * något och inte tappar bort vart man var på väg.
 * </para>
 */

/** En token med formen huvud.payload.signatur, där mitten bär adressen. */
const SIGNED_IN_TOKEN = `x.${btoa('{"email":"foralder@example.com"}')}.y`

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as unknown as Response
}

/** Svarar som inloggnings-API:t, med valfritt utfall på verifieringen. */
function stubAuth(options: { verify?: 'ok' | 'fel'; refresh?: 'ok' } = {}) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/request-code')) return Promise.resolve(jsonResponse(null, 202))

      if (url.includes('/auth/verify-code')) {
        return Promise.resolve(
          options.verify === 'fel'
            ? jsonResponse({ title: 'Koden stämmer inte' }, 401)
            : jsonResponse({ accessToken: SIGNED_IN_TOKEN }),
        )
      }

      if (url.includes('/auth/logout')) return Promise.resolve(jsonResponse(null, 204))
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(
          options.refresh === 'ok'
            ? jsonResponse({ accessToken: SIGNED_IN_TOKEN })
            : jsonResponse({ title: 'Nej' }, 401),
        )
      }

      return Promise.resolve(jsonResponse({ teams: [] }))
    }),
  )
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('inloggningsvyn', () => {
  it('börjar med att fråga efter mejladressen', async () => {
    stubAuth()

    renderRoute('/logga-in')

    expect(await screen.findByRole('heading', { name: 'Logga in' })).toBeInTheDocument()
    expect(screen.getByLabelText('Mejladress')).toBeInTheDocument()
  })

  it('säger på svenska när adressen ser fel ut', async () => {
    stubAuth()

    const user = userEvent.setup()
    renderRoute('/logga-in')

    await user.type(await screen.findByLabelText('Mejladress'), 'inte-en-adress')
    await user.click(screen.getByRole('button', { name: 'Skicka kod' }))

    expect(await screen.findByText('Mejladressen ser inte riktig ut.')).toBeInTheDocument()
  })

  it('avslöjar inte om adressen finns hos oss', async () => {
    // Servern svarar likadant oavsett, och texten får inte säga mer än servern gör.
    stubAuth()

    const user = userEvent.setup()
    renderRoute('/logga-in')

    await user.type(await screen.findByLabelText('Mejladress'), 'foralder@example.com')
    await user.click(screen.getByRole('button', { name: 'Skicka kod' }))

    expect(await screen.findByRole('status')).toHaveTextContent(
      /Om foralder@example\.com finns hos oss/,
    )
  })

  it('säger till på svenska när koden är fel', async () => {
    stubAuth({ verify: 'fel' })

    const user = userEvent.setup()
    renderRoute('/logga-in')

    await user.type(await screen.findByLabelText('Mejladress'), 'foralder@example.com')
    await user.click(screen.getByRole('button', { name: 'Skicka kod' }))

    await user.type(await screen.findByLabelText('Kod från mejlet'), '000000')
    await user.click(screen.getByRole('button', { name: 'Logga in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      /Koden stämmer inte, eller har gått ut/,
    )
  })

  it('kräver sex siffror innan den ens frågar servern', async () => {
    // Ett stavfel ska inte kosta ett av de fem försöken.
    stubAuth()

    const user = userEvent.setup()
    renderRoute('/logga-in')

    await user.type(await screen.findByLabelText('Mejladress'), 'foralder@example.com')
    await user.click(screen.getByRole('button', { name: 'Skicka kod' }))

    await user.type(await screen.findByLabelText('Kod från mejlet'), '123')
    await user.click(screen.getByRole('button', { name: 'Logga in' }))

    expect(await screen.findByText('Koden är sex siffror.')).toBeInTheDocument()
  })
})

describe('skyddade routes', () => {
  it('skickar en utloggad till inloggningen', async () => {
    stubAuth()

    const { router } = renderRoute('/konto')

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/logga-in')
    })
  })

  it('minns vart man var på väg', async () => {
    stubAuth()

    const { router } = renderRoute('/konto')

    await waitFor(() => {
      expect(router.state.location.search).toEqual({ next: '/konto' })
    })
  })

  it('släpper in den som redan är inloggad', async () => {
    /*
     * Det som gor en atervandande foralder inloggad ar cookien, inte nagot appen sparat.
     * Darfor sags stubben ja pa fornyelsen, och ledtraden satts som en tidigare
     * inloggning skulle ha gjort.
     */
    stubAuth({ refresh: 'ok' })
    setAccessToken(SIGNED_IN_TOKEN)

    renderRoute('/konto')

    expect(await screen.findByRole('heading', { name: 'Mitt konto' })).toBeInTheDocument()
    expect(screen.getByText(/foralder@example.com/)).toBeInTheDocument()
  })

  it('vägrar skicka vidare till en annan webbplats', async () => {
    /*
     * En open redirect ar som dyrast precis efter en inloggning: den som klickat pa
     * lanken litar pa sidan hen kommer till.
     *
     * Testet prover beteendet och inte adressfaltet. Skyddet ligger i den *validerade*
     * sokstrangen komponenten laser -- den rada adressen far garna innehalla vad som
     * helst, sa lange ingen foljer det.
     */
    stubAuth()

    const user = userEvent.setup()
    const { router } = renderRoute('/logga-in?next=https://annan-sajt.example')

    await user.type(await screen.findByLabelText('Mejladress'), 'foralder@example.com')
    await user.click(screen.getByRole('button', { name: 'Skicka kod' }))

    await user.type(await screen.findByLabelText('Kod från mejlet'), '123456')
    await user.click(screen.getByRole('button', { name: 'Logga in' }))

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/')
    })

    expect(router.state.location.href).not.toContain('annan-sajt')
  })
})
