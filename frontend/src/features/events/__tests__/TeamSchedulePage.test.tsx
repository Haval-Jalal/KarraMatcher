import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { stubApi } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Schemasidans skötsel-affordans (`#287`).
 *
 * En tränare gäller hela truppen, så "Sköt laget" ska nå trupp-tränaren på vilket som helst
 * av truppens färg-lag — inte bara det lag hen råkar bära ett per-lag-anspråk för. Länken är
 * bara UX; servern är grinden. Därför prövas här bara att rätt roll ser rätt affordans.
 */

/** Token för en trupp-tränare (admin för hela truppen). Stubben svarar med truppId 'trupp-stub'. */
function truppCoachToken(truppId: string): string {
  return `x.${btoa(JSON.stringify({ email: 'tranare@example.com', 'admin-trupp': truppId }))}.y`
}

/** Token för en inloggad vårdnadshavare utan någon ledarroll. */
function guardianToken(): string {
  return `x.${btoa(JSON.stringify({ email: 'foralder@example.com' }))}.y`
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('skötsel-länken på lagets schema', () => {
  it('visar "Sköt laget" för en trupp-tränare på ett av truppens färg-lag', async () => {
    stubApi({})
    const token = truppCoachToken('trupp-stub')
    setAccessToken(token)

    renderRoute('/lag/gul')

    expect(await screen.findByRole('link', { name: 'Sköt laget' })).toHaveAttribute(
      'href',
      '/lag/gul/tranare',
    )
  })

  it('döljer "Sköt laget" för en vårdnadshavare utan ledarroll', async () => {
    stubApi({})
    setAccessToken(guardianToken())

    renderRoute('/lag/gul')

    // Schemat ska ha hunnit fram innan vi drar slutsatsen att länken inte finns.
    expect(await screen.findByRole('heading', { name: 'Truppen' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Sköt laget' })).not.toBeInTheDocument()
  })
})
