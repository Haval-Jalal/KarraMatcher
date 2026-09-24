import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Superadmins uppsättningsguide (§KM.3, `#192`, `#261`).
 *
 * Guiden är en kedja: Sport → Klubb → Trupp → tilldela admin. Testerna vaktar att man kan
 * skapa en sport i steg 1 (och att anropet bär rätt data, med slug föreslagen ur namnet),
 * att en ogiltig slug stoppas i klienten, att guiden går vidare, och att en vanlig inloggad
 * inte ens ser vyn.
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
      if (url.includes('/auth/refresh'))
        return Promise.resolve(jsonResponse({ accessToken: token }))

      if (url.includes('/api/v1/admin/sports')) {
        if (method === 'POST') {
          const parsed = JSON.parse(init?.body as string) as { name: string; slug: string }
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

describe('Uppsättningsguiden', () => {
  it('skapar en sport i steg 1 och väljer den — anropet bär rätt data', async () => {
    const user = userEvent.setup()
    const { sent } = stubApi(superToken)
    setAccessToken(superToken)

    renderRoute('/superadmin')

    // Steg 1 är sport. Skapa-formuläret ligger bakom en knapp; sluggen föreslås ur namnet.
    await user.click(await screen.findByRole('button', { name: 'Skapa ny sport' }))
    await user.type(screen.getByLabelText('Namn'), 'Fotboll')
    expect(screen.getByLabelText(/Slug/)).toHaveValue('fotboll')
    await user.click(screen.getByRole('button', { name: 'Skapa sport' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/admin/sports') &&
            r.method === 'POST' &&
            (r.body as { slug: string }).slug === 'fotboll',
        ),
      ).toBe(true)
    })

    // Den skapade sporten dyker upp som ett valt alternativ, och "Nästa" blir möjlig.
    const choice = await screen.findByRole('radio', { name: /Fotboll/ })
    expect(choice).toBeChecked()
    expect(screen.getByRole('button', { name: 'Nästa' })).toBeEnabled()
  })

  it('går vidare till klubb-steget när en sport valts', async () => {
    const user = userEvent.setup()
    stubApi(superToken)
    setAccessToken(superToken)

    renderRoute('/superadmin')

    await user.click(await screen.findByRole('button', { name: 'Skapa ny sport' }))
    await user.type(screen.getByLabelText('Namn'), 'Fotboll')
    await user.click(screen.getByRole('button', { name: 'Skapa sport' }))

    await screen.findByRole('radio', { name: /Fotboll/ })
    await user.click(screen.getByRole('button', { name: 'Nästa' }))

    expect(await screen.findByText('Steg 2 av 4 — Klubb')).toBeInTheDocument()
  })

  it('avvisar en ogiltig slug i klienten utan att något skickas', async () => {
    const user = userEvent.setup()
    const { sent } = stubApi(superToken)
    setAccessToken(superToken)

    renderRoute('/superadmin')

    await user.click(await screen.findByRole('button', { name: 'Skapa ny sport' }))
    await user.type(screen.getByLabelText('Namn'), 'Fotboll')
    await user.clear(screen.getByLabelText(/Slug/))
    await user.type(screen.getByLabelText(/Slug/), 'Med Mellanslag')
    await user.click(screen.getByRole('button', { name: 'Skapa sport' }))

    expect(await screen.findByText(/Bara små bokstäver/)).toBeInTheDocument()
    expect(sent.some((r) => r.method === 'POST' && r.url.includes('/api/v1/admin/sports'))).toBe(
      false,
    )
  })

  it('en vanlig inloggad ser inte konsolen', async () => {
    stubApi(plainToken)
    setAccessToken(plainToken)

    renderRoute('/superadmin')

    expect(await screen.findByText('Den här vyn är bara för superadmin.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Skapa ny sport' })).not.toBeInTheDocument()
  })
})
