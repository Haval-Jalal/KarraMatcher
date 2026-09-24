import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Ansökningar (§KM.3, `#194`), ur användarens perspektiv: en förälder ansöker via länken, och
 * en trupp-admin godkänner i kön.
 */

function tokenWith(claims: Record<string, unknown>): string {
  return `x.${btoa(JSON.stringify(claims))}.y`
}

interface Sent {
  url: string
  method: string
}

function stub(token: string, routes: (url: string, method: string) => unknown): Sent[] {
  const sent: Sent[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'
      sent.push({ url, method })

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      // Tomt token = utloggad: förnyelsen mot cookien svarar 401, och gästen möts av
      // inloggningen (`#255`). Annars ger den access-token som en inloggad medlem.
      if (url.includes('/auth/refresh')) {
        return Promise.resolve(
          token === ''
            ? jsonResponse({ title: 'Ingen session' }, 401)
            : jsonResponse({ accessToken: token }),
        )
      }

      return Promise.resolve(jsonResponse(routes(url, method)))
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

describe('Ansökningssidan', () => {
  it('en inloggad kan ansöka', async () => {
    const user = userEvent.setup()
    const token = tokenWith({ email: 'sokande@example.com' })
    const sent = stub(token, (url) => {
      if (url.includes('/apply-info')) return { truppName: 'P2016' }
      return {}
    })
    setAccessToken(token)

    renderRoute('/ansok/trupp-1')

    await user.click(await screen.findByRole('button', { name: 'Ansök om att gå med' }))

    expect(await screen.findByRole('heading', { name: 'Ansökan inskickad' })).toBeInTheDocument()
    expect(
      sent.some(
        (r) => r.url.includes('/api/v1/trupper/trupp-1/applications') && r.method === 'POST',
      ),
    ).toBe(true)
  })

  it('en utloggad ombeds logga in', async () => {
    stub('', (url) => {
      if (url.includes('/apply-info')) return { truppName: 'P2016' }
      return {}
    })

    renderRoute('/ansok/trupp-1')

    // Sidans egen uppmaning (skild från menyns "Logga in"-länk).
    expect(await screen.findByText('Logga in för att ansöka.')).toBeInTheDocument()
  })
})

describe('Ansökningskön', () => {
  it('admin kan godkänna en ansökan', async () => {
    const user = userEvent.setup()
    const token = tokenWith({ email: 'admin@example.com', 'admin-trupp': 'trupp-1' })
    const sent = stub(token, (url, method) => {
      if (url.includes('/my-trupper')) {
        return [
          { id: 'trupp-1', clubName: 'Kärra', sportName: 'Fotboll', name: 'P2016', season: '2026' },
        ]
      }
      if (url.includes('/applications') && method === 'GET') {
        return [
          {
            id: 'app-1',
            applicantName: 'Anna',
            applicantEmail: 'anna@example.com',
            status: 'Pending',
            createdUtc: '2026-09-16T10:00:00Z',
          },
        ]
      }
      return []
    })
    setAccessToken(token)

    renderRoute('/admin')

    await user.selectOptions(await screen.findByLabelText('Välj trupp'), 'trupp-1')

    const heading = await screen.findByRole('heading', { name: 'Ansökningar' })
    const section = heading.closest('.admin-subsection') as HTMLElement

    await user.click(within(section).getByRole('button', { name: 'Godkänn' }))

    await waitFor(() => {
      expect(
        sent.some(
          (r) =>
            r.url.includes('/api/v1/admin/trupper/trupp-1/applications/app-1/approve') &&
            r.method === 'POST',
        ),
      ).toBe(true)
    })
  })
})
