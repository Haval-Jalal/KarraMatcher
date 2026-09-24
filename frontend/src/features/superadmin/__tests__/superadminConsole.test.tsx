import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Superadmin-konsolen (§KM.3, `#192`).
 *
 * Två saker vaktas ur användarens perspektiv: att superadmin kan skapa en sport (och att
 * anropet bär rätt data), och att en vanlig inloggad inte ens ser vyn — den riktiga grinden
 * är serverns, men vyn ska inte heller visa sig.
 */

function tokenWith(claims: Record<string, unknown>): string {
  return `x.${btoa(JSON.stringify(claims))}.y`
}

const superToken = tokenWith({ email: 'super@example.com', superadmin: 'true' })
const plainToken = tokenWith({ email: 'foralder@example.com' })

/** Fångar anropen och svarar per adress. Sport-listan växer när en sport skapas. */
function stubApi(token: string) {
  const sent: { url: string; method: string; body: unknown }[] = []
  const sports: { id: string; name: string; slug: string }[] = []

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
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(jsonResponse({ accessToken: token }))
      }

      if (url.includes('/api/v1/admin/sports')) {
        if (method === 'POST') {
          const body = init?.body as string
          const parsed = JSON.parse(body) as { name: string; slug: string }
          const created = { id: 'sport-1', name: parsed.name, slug: parsed.slug }
          sports.push(created)
          return Promise.resolve(jsonResponse(created, 201))
        }

        return Promise.resolve(jsonResponse(sports))
      }

      if (url.includes('/api/v1/admin/clubs')) return Promise.resolve(jsonResponse([]))
      if (url.includes('/api/v1/admin/trupper')) return Promise.resolve(jsonResponse([]))

      return Promise.resolve(jsonResponse([]))
    }),
  )

  return { sent }
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('Superadmin-konsolen', () => {
  it('superadmin kan skapa en sport, och anropet bär rätt data', async () => {
    const user = userEvent.setup()
    const { sent } = stubApi(superToken)
    setAccessToken(superToken)

    renderRoute('/superadmin')

    const section = await screen.findByRole('region', { name: 'Sporter' })

    // Skapa-formuläret ligger bakom en knapp (`#251`).
    await user.click(within(section).getByRole('button', { name: 'Lägg till sport' }))
    await user.type(within(section).getByLabelText('Namn'), 'Fotboll')
    await user.type(within(section).getByLabelText(/Slug/), 'fotboll')
    await user.click(within(section).getByRole('button', { name: 'Spara' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/admin/sports') &&
            r.method === 'POST' &&
            (r.body as { name: string; slug: string }).slug === 'fotboll',
        ),
      ).toBe(true)
    })

    // Listan speglar servern efter skapandet.
    expect(await within(section).findByText('Fotboll')).toBeInTheDocument()
  })

  it('flikarna visar en sektion i taget', async () => {
    const user = userEvent.setup()
    stubApi(superToken)
    setAccessToken(superToken)

    renderRoute('/superadmin')

    // Sporter är öppen som standard; Klubbar ligger dold bakom sin flik.
    expect(await screen.findByRole('region', { name: 'Sporter' })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Klubbar' })).not.toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: 'Klubbar' }))

    expect(await screen.findByRole('region', { name: 'Klubbar' })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Sporter' })).not.toBeInTheDocument()
  })

  it('en vanlig inloggad ser inte konsolen', async () => {
    stubApi(plainToken)
    setAccessToken(plainToken)

    renderRoute('/superadmin')

    expect(await screen.findByText('Den här vyn är bara för superadmin.')).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Sporter' })).not.toBeInTheDocument()
  })

  it('en ogiltig slug avvisas i klienten utan att något skickas', async () => {
    const user = userEvent.setup()
    const { sent } = stubApi(superToken)
    setAccessToken(superToken)

    renderRoute('/superadmin')

    const section = await screen.findByRole('region', { name: 'Sporter' })

    await user.click(within(section).getByRole('button', { name: 'Lägg till sport' }))
    await user.type(within(section).getByLabelText('Namn'), 'Fotboll')
    await user.type(within(section).getByLabelText(/Slug/), 'Med Mellanslag')
    await user.click(within(section).getByRole('button', { name: 'Spara' }))

    expect(await within(section).findByText(/Bara små bokstäver/)).toBeInTheDocument()
    expect(sent.some((r) => r.method === 'POST' && r.url.includes('/api/v1/admin/sports'))).toBe(
      false,
    )
  })
})
