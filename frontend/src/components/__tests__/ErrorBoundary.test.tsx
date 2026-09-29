import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { ErrorBoundary } from '../ErrorBoundary'

/**
 * Yttersta skyddsnätet (#389): ett kast utanför en route ska ge ett svenskt fallback,
 * inte en vit skärm — och felet ska loggas, inte sväljas tyst.
 */
function Boom(): never {
  throw new Error('kaboom')
}

describe('ErrorBoundary', () => {
  beforeEach(() => {
    // React skriver själv ut det fångade felet till konsolen — tysta det så testloggen är ren.
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('visar barnen när inget kastar', () => {
    render(
      <ErrorBoundary>
        <p>Allt fungerar</p>
      </ErrorBoundary>,
    )

    expect(screen.getByText('Allt fungerar')).toBeInTheDocument()
  })

  it('visar ett svenskt fallback och loggar när ett barn kastar', () => {
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    )

    expect(screen.getByRole('heading', { name: 'Något gick fel' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ladda om sidan' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Till startsidan' })).toHaveAttribute('href', '/')
    // Felet loggades (componentDidCatch → logClientError), inte svaldes.
    expect(console.error).toHaveBeenCalled()
  })
})
