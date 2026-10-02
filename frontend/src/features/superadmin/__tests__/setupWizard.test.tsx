import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderWithProviders } from '@/test/renderWithProviders'

import { SetupWizard } from '../SetupWizard'

/**
 * Superadmins uppsättningsguide (`#261`). Det som vaktas här (#540): ett byte uppströms
 * (sport/klubb) får inte lämna ett kvarglömt val nedströms som låter superadmin gå vidare och
 * tilldela en tränare till fel klubbs trupp.
 */

const SPORTS = [{ id: 'sp1', name: 'Fotboll', slug: 'fotboll' }]
const CLUBS = [
  { id: 'A', name: 'Klubb A', slug: 'a' },
  { id: 'B', name: 'Klubb B', slug: 'b' },
]
const TRUPPER = [
  {
    id: 'T',
    clubId: 'A',
    clubName: 'Klubb A',
    sportId: 'sp1',
    sportName: 'Fotboll',
    name: 'P2016',
    season: '2026',
  },
]

function stub(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)
      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/admin/sports')) return Promise.resolve(jsonResponse(SPORTS))
      if (url.includes('/admin/clubs')) return Promise.resolve(jsonResponse(CLUBS))
      if (url.includes('/admin/trupper')) return Promise.resolve(jsonResponse(TRUPPER))
      return Promise.resolve(jsonResponse([]))
    }),
  )
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  setAccessToken(`x.${btoa(JSON.stringify({ email: 'super@example.com', superadmin: true }))}.y`)
  stub()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('SetupWizard nollar nedströmsval när klubben byts (#540)', () => {
  it('byte av klubb låser Nästa på trupp-steget tills en trupp i rätt klubb valts', async () => {
    const user = userEvent.setup()
    await renderWithProviders(<SetupWizard />)

    // Steg 1: välj sport → Nästa.
    await user.click(await screen.findByRole('radio', { name: /Fotboll/ }))
    await user.click(screen.getByRole('button', { name: 'Nästa' }))

    // Steg 2: välj Klubb A → Nästa.
    await user.click(await screen.findByRole('radio', { name: /Klubb A/ }))
    await user.click(screen.getByRole('button', { name: 'Nästa' }))

    // Steg 3: truppen P2016 (klubb A) går att välja och Nästa blir aktiv.
    await user.click(await screen.findByRole('radio', { name: /P2016/ }))
    expect(screen.getByRole('button', { name: 'Nästa' })).toBeEnabled()

    // Gå tillbaka till steg 2 och byt till Klubb B.
    await user.click(screen.getByRole('button', { name: 'Tillbaka' }))
    await user.click(await screen.findByRole('radio', { name: /Klubb B/ }))
    await user.click(screen.getByRole('button', { name: 'Nästa' }))

    // Steg 3 igen: Klubb B har ingen trupp, och det stale truppId:t från Klubb A är nollat →
    // Nästa är låst, så superadmin kan inte tilldela en tränare till fel klubbs trupp.
    await waitFor(() => expect(screen.getByRole('button', { name: 'Nästa' })).toBeDisabled())
    expect(screen.queryByRole('radio', { name: /P2016/ })).not.toBeInTheDocument()
  })

  it('kopplar "skapa"-formulärets valideringsfel till fältet (#600)', async () => {
    const user = userEvent.setup()
    await renderWithProviders(<SetupWizard />)

    await user.click(await screen.findByRole('button', { name: 'Skapa ny sport' }))
    // Submit med tomt namn → zod-fel. Felet ska vara kopplat till Namn-fältet, inte bara stå löst.
    await user.click(screen.getByRole('button', { name: 'Skapa sport' }))

    await waitFor(() =>
      expect(screen.getByLabelText('Namn')).toHaveAccessibleDescription(/Fyll i namnet/),
    )
  })
})
