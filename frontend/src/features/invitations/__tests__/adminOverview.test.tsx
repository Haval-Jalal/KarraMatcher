import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { jsonResponse } from '@/test/apiStub'
import { renderWithProviders } from '@/test/renderWithProviders'

import { AdminOverview } from '../AdminOverview'

/**
 * Adminöversikten (`#381`): nyckeltalen får aldrig visa en påhittad nolla när en hämtning
 * misslyckas — "0 barn" när nätet dog vilseleder. Då visas ett tillstånd i stället.
 */

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('AdminOverview', () => {
  it('visar ett felbesked i stället för nollor när en hämtning misslyckas', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown) => {
        const url = String(input)
        if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
        return Promise.resolve(jsonResponse({ title: 'fel' }, 500))
      }),
    )

    await renderWithProviders(<AdminOverview truppId="t1" onGotoApplications={() => undefined} />)

    expect(await screen.findByRole('alert')).toHaveTextContent(/Kunde inte hämta översikten/)
    expect(screen.queryByText('barn')).not.toBeInTheDocument()
  })

  it('säger till om nätet är borta', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn((input: unknown) => {
        const url = String(input)
        if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
        return Promise.reject(new TypeError('Failed to fetch'))
      }),
    )

    await renderWithProviders(<AdminOverview truppId="t1" onGotoApplications={() => undefined} />)

    expect(await screen.findByRole('alert')).toHaveTextContent(/Ingen anslutning/)
  })
})
