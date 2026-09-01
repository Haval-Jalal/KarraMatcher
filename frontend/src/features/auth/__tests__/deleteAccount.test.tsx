import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { DeleteAccountSection } from '@/features/auth'
import { clearSession, getAccessToken, setAccessToken } from '@/lib/session'

/**
 * Radera kontot, från gränssnittets sida (`#33`, §KM.6).
 *
 * <para>
 * Två saker vaktas här. Att bekräftelsen inte går att klicka igenom av misstag — det
 * finns ingen ångerknapp. Och att texten är ärlig om spelarkortet, som ligger kvar i
 * telefonen eftersom det aldrig nått servern (§KM.2). En förälder ska varken tro att
 * statistiken försvann eller bli förvånad över att den finns kvar.
 * </para>
 */

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as unknown as Response
}

function stubApi(options: { deleteFails?: boolean } = {}) {
  const calls: string[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)
      calls.push(`${init?.method ?? 'GET'} ${url}`)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))

      return Promise.resolve(
        options.deleteFails ? jsonResponse({ title: 'Nej' }, 500) : jsonResponse(null, 204),
      )
    }),
  )

  return calls
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('bekräftelsen går inte att klicka igenom', () => {
  it('raderar ingenting på första klicket', async () => {
    const calls = stubApi()
    const user = userEvent.setup()

    render(<DeleteAccountSection onDeleted={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Radera mitt konto' }))

    expect(calls.filter((call) => call.startsWith('DELETE'))).toHaveLength(0)
  })

  it('kräver ett andra, annorlunda klick', async () => {
    // Knappen i steg två heter något annat och står inte där den första gjorde. En
    // dubbelklickning på samma ställe ska inte kunna radera ett konto.
    const calls = stubApi()
    const user = userEvent.setup()

    render(<DeleteAccountSection onDeleted={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Radera mitt konto' }))
    await user.click(await screen.findByRole('button', { name: 'Ja, radera kontot' }))

    expect(calls.filter((call) => call.startsWith('DELETE'))).toHaveLength(1)
  })

  it('går att ångra innan det är gjort', async () => {
    const calls = stubApi()
    const user = userEvent.setup()

    render(<DeleteAccountSection onDeleted={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Radera mitt konto' }))
    await user.click(await screen.findByRole('button', { name: 'Avbryt' }))

    expect(screen.getByRole('button', { name: 'Radera mitt konto' })).toBeInTheDocument()
    expect(calls.filter((call) => call.startsWith('DELETE'))).toHaveLength(0)
  })
})

describe('texten är ärlig om spelarkortet', () => {
  it('säger att statistiken ligger kvar i telefonen', async () => {
    // §KM.2: den har aldrig nått servern, så den kan inte raderas härifrån. Att inte
    // säga det hade varit att låta en förälder tro att den försvann.
    stubApi()
    const user = userEvent.setup()

    render(<DeleteAccountSection onDeleted={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Radera mitt konto' }))

    expect(await screen.findByText(/Spelarkortet påverkas inte/)).toBeInTheDocument()
    expect(screen.getByText(/aldrig legat på servern/)).toBeInTheDocument()
  })

  it('räknar upp vad som faktiskt försvinner', async () => {
    stubApi()
    const user = userEvent.setup()

    render(<DeleteAccountSection onDeleted={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Radera mitt konto' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/samåkningar/)
  })
})

describe('efter raderingen', () => {
  it('glömmer sessionen', async () => {
    stubApi()
    setAccessToken('en.token')
    const user = userEvent.setup()

    render(<DeleteAccountSection onDeleted={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Radera mitt konto' }))
    await user.click(await screen.findByRole('button', { name: 'Ja, radera kontot' }))

    expect(getAccessToken()).toBeNull()
  })

  it('glömmer sessionen även när anropet misslyckas', async () => {
    // Står appen kvar som inloggad blir nästa anrop ett obegripligt fel.
    stubApi({ deleteFails: true })
    setAccessToken('en.token')
    const user = userEvent.setup()

    render(<DeleteAccountSection onDeleted={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'Radera mitt konto' }))
    await user.click(await screen.findByRole('button', { name: 'Ja, radera kontot' }))

    expect(getAccessToken()).toBeNull()
    expect(await screen.findByText(/Kunde inte radera kontot/)).toBeInTheDocument()
  })
})

describe('CSRF-token hämtas om när inloggningen ändras', () => {
  it('cachar inte en token över en inloggning', async () => {
    /*
     * ASP.NET binder anti-forgery-token till identiteten. En token hamtad utloggad galler
     * inte for ett inloggat anrop -- det upptacktes genom att raderingen svarade 400 i
     * integrationstesterna, och hade slagit till i drift pa exakt samma satt.
     */
    const calls = stubApi()

    setAccessToken('forsta.token')
    await import('@/lib/api').then((api) => api.renewSession())

    const before = calls.filter((call) => call.includes('/auth/csrf')).length

    setAccessToken('andra.token')
    await import('@/lib/api').then((api) => api.renewSession())

    const after = calls.filter((call) => call.includes('/auth/csrf')).length

    expect(after).toBeGreaterThan(before)
  })
})
