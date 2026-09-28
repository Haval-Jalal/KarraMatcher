import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'

import { ThemeToggle } from '../ThemeToggle'

/**
 * Tema-väljaren (`#redesign`): System / Ljust / Mörkt.
 *
 * Prövar kontraktet mot `<html data-theme>` och `localStorage` — att System inte sätter något
 * attribut (media-frågan gäller), att ett eget val skrivs som attribut och sparas, och att
 * tillbaka till System tar bort attributet igen.
 */

beforeEach(() => {
  localStorage.clear()
  document.documentElement.removeAttribute('data-theme')
})

afterEach(() => {
  localStorage.clear()
  document.documentElement.removeAttribute('data-theme')
})

describe('tema-väljaren', () => {
  it('börjar på System och sätter inget data-theme', () => {
    render(<ThemeToggle />)

    expect(screen.getByLabelText('System')).toBeChecked()
    expect(document.documentElement.hasAttribute('data-theme')).toBe(false)
  })

  it('väljer Mörkt → sätter data-theme och sparar valet på enheten', async () => {
    const user = userEvent.setup()
    render(<ThemeToggle />)

    await user.click(screen.getByLabelText('Mörkt'))

    expect(document.documentElement.getAttribute('data-theme')).toBe('dark')
    expect(localStorage.getItem('karra.theme')).toBe('dark')
  })

  it('tillbaka till System → tar bort data-theme igen', async () => {
    const user = userEvent.setup()
    render(<ThemeToggle />)

    await user.click(screen.getByLabelText('Mörkt'))
    await user.click(screen.getByLabelText('System'))

    expect(document.documentElement.hasAttribute('data-theme')).toBe(false)
    expect(localStorage.getItem('karra.theme')).toBe('system')
  })
})
