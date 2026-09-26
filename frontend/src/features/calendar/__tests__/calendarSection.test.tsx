import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { CalendarSection } from '@/features/calendar'
import { jsonResponse } from '@/test/apiStub'
import { renderWithProviders } from '@/test/renderWithProviders'

/**
 * Kalender-sektionen på Mitt konto.
 *
 * Vaktar att medlemmen ser sin länk att prenumerera på, och kan byta ut den — varpå den nya
 * länken visas i stället för den gamla.
 */

const FIRST = 'https://app.example/api/v1/kalender/tok1.ics'
const SECOND = 'https://app.example/api/v1/kalender/tok2.ics'

function stub() {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/api/v1/kalender/aterkalla') && method === 'POST') {
        return Promise.resolve(jsonResponse({ url: SECOND }))
      }
      if (url.includes('/api/v1/kalender/min')) return Promise.resolve(jsonResponse({ url: FIRST }))

      return Promise.resolve(jsonResponse({}))
    }),
  )
}

beforeEach(() => {
  stub()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('kalender-sektionen', () => {
  it('visar länken att prenumerera på', async () => {
    await renderWithProviders(<CalendarSection />)

    expect(await screen.findByLabelText('Din kalender-länk')).toHaveValue(FIRST)
    expect(screen.getByRole('button', { name: 'Kopiera' })).toBeInTheDocument()
  })

  it('byter ut länken och visar den nya', async () => {
    const user = userEvent.setup()
    await renderWithProviders(<CalendarSection />)

    await screen.findByLabelText('Din kalender-länk')

    await user.click(screen.getByRole('button', { name: 'Skapa ny länk…' }))
    await user.click(screen.getByRole('button', { name: 'Ja, skapa ny länk' }))

    await waitFor(() => {
      expect(screen.getByLabelText('Din kalender-länk')).toHaveValue(SECOND)
    })
  })
})
