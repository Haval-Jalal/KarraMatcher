import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'

import { LagPanel } from '../LagPanel'

/**
 * Lag-panelen (`#192`, `#261`). Vaktar att den nu ligger under truppens adress
 * (`/admin/trupper/{id}/lag`, admins ansvar), att sluggen föreslås ur namnet, och att ett
 * kvitto visas efter sparat.
 */

interface Sent {
  url: string
  method: string
  body: unknown
}

function stub() {
  const sent: Sent[] = []

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
      if (url.includes('/lag') && method === 'POST') {
        return Promise.resolve(
          jsonResponse(
            { id: 'l1', truppId: 't1', name: 'Gul', colorHex: '#D9A21B', slug: 'gul' },
            201,
          ),
        )
      }
      if (url.includes('/lag')) return Promise.resolve(jsonResponse([]))
      return Promise.resolve(jsonResponse([]))
    }),
  )

  return { sent }
}

function renderPanel() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } })
  return render(
    <QueryClientProvider client={client}>
      <LagPanel truppId="t1" />
    </QueryClientProvider>,
  )
}

beforeEach(() => {
  localStorage.clear()
  setAccessToken('test-token')
})

afterEach(() => {
  vi.unstubAllGlobals()
  clearSession()
})

describe('LagPanel', () => {
  it('skapar ett lag under truppens adress — slug ur namnet, med kvitto', async () => {
    const { sent } = stub()
    const user = userEvent.setup()

    renderPanel()

    await user.type(await screen.findByLabelText(/Namn/), 'Gul')
    expect(screen.getByLabelText(/Slug/)).toHaveValue('gul')

    await user.click(screen.getByRole('button', { name: 'Lägg till lag' }))

    await waitFor(() => {
      const post = sent.find(
        (r) => r.method === 'POST' && r.url.includes('/api/v1/admin/trupper/t1/lag'),
      )
      expect(post?.body).toMatchObject({ name: 'Gul', slug: 'gul', colorHex: '#d9a21b' })
    })

    expect(await screen.findByText('Gul sparades.')).toBeInTheDocument()
  })
})
