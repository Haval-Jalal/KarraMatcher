import { screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { emptyResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Integritetstexten (`#66`, §KM.6).
 *
 * <para>
 * Testet vaktar det som är själva poängen med sidan: att den säger de obekväma sakerna rakt
 * ut. Att spelarkortet stannar i telefonen, att samåkningen gallras, och framför allt att
 * mejladressen lämnar EU — med leverantören och grunden namngivna, inte gömda bakom
 * "underleverantörer". Och att en gäst kan nå sidan utan att logga in.
 * </para>
 */

afterEach(() => {
  vi.unstubAllGlobals()
})

/** Sidan hämtar ingen data. Auth-lagret försöker ändå läsa sessionen vid start. */
function stubNoSession() {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => Promise.resolve(emptyResponse(401))),
  )
}

describe('PrivacyPage', () => {
  it('nås utan inloggning och visar rubriken', async () => {
    stubNoSession()
    renderRoute('/integritet')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Så hanteras dina uppgifter' }),
    ).toBeInTheDocument()
  })

  it('säger att spelarkortet stannar på enheten och kan gå förlorat', async () => {
    stubNoSession()
    renderRoute('/integritet')

    await screen.findByRole('heading', { level: 1 })

    expect(screen.getByText(/bara på den här telefonen/i)).toBeInTheDocument()
    expect(screen.getByText(/säkerhetskopieringskoden/i)).toBeInTheDocument()
  })

  it('säger att samåkningen gallras efter 30 dagar', async () => {
    stubNoSession()
    renderRoute('/integritet')

    await screen.findByRole('heading', { level: 1 })

    expect(screen.getByText(/raderas 30 dagar efter matchen/i)).toBeInTheDocument()
  })

  it('anger rakt ut att mejladressen lämnar EU, med leverantör och grund namngivna', async () => {
    stubNoSession()
    renderRoute('/integritet')

    await screen.findByRole('heading', { level: 2, name: 'En uppgift lämnar EU' })

    expect(screen.getByText('Resend')).toBeInTheDocument()
    expect(screen.getByText('USA')).toBeInTheDocument()
    expect(screen.getByText(/Data Privacy Framework/)).toBeInTheDocument()
  })

  it('beskriver barnets minimala profil på servern och inte det gamla "inga uppgifter om barn" (#595)', async () => {
    // Efter v2 (`#189`) lagrar servern en minimal barn-profil (§KM.1). Sidan måste säga det rakt
    // ut, inte påstå motsatsen.
    stubNoSession()
    renderRoute('/integritet')

    await screen.findByRole('heading', { level: 2, name: 'Barnet i truppen' })

    expect(screen.getByText(/efternamnets första bokstav/i)).toBeInTheDocument()
    expect(screen.getByText(/godkänt det/i)).toBeInTheDocument()
    // Det gamla, falska påståendet får inte stå kvar någonstans.
    expect(screen.queryByText(/inga uppgifter om barn/i)).not.toBeInTheDocument()
  })

  it('säger att appen inte spårar', async () => {
    stubNoSession()
    renderRoute('/integritet')

    await screen.findByRole('heading', { level: 1 })

    expect(screen.getByText(/inga spårningsskript/i)).toBeInTheDocument()
  })

  it('nås via Mer-menyn, även som gäst', async () => {
    // Integritetslänken flyttades från foten till Mer (`#redesign`). Mer är publik → gästen når den.
    stubNoSession()
    renderRoute('/mer')

    await waitFor(() => {
      expect(screen.getByRole('link', { name: 'Så hanteras dina uppgifter' })).toBeInTheDocument()
    })
  })
})
