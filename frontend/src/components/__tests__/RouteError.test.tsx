import type * as ReactRouter from '@tanstack/react-router'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ComponentProps } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

// Link kräver egentligen en router-kontext; här räcker en vanlig ankare för att pröva
// RouteErrors egen text och beteende isolerat.
vi.mock('@tanstack/react-router', async (importOriginal) => {
  const actual = await importOriginal<typeof ReactRouter>()
  return {
    ...actual,
    Link: ({ to, children, ...rest }: ComponentProps<'a'> & { to: string }) => (
      <a href={to} {...rest}>
        {children}
      </a>
    ),
  }
})

const { RouteError } = await import('../RouteError')

/**
 * Route-intern felgräns (#389): svenskt UI (§KM.9), en retry via `reset`, och felet loggas
 * (§KM.6/§KM.10) i stället för TanStacks engelska, ologgade standard.
 */
describe('RouteError', () => {
  beforeEach(() => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('visar svensk text och loggar felet', () => {
    render(<RouteError error={new Error('trasig loader')} reset={() => {}} />)

    expect(screen.getByRole('heading', { name: 'Något gick fel' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Till startsidan' })).toHaveAttribute('href', '/')
    expect(console.error).toHaveBeenCalled()
  })

  it('retry-knappen anropar reset', async () => {
    const reset = vi.fn()
    const user = userEvent.setup()

    render(<RouteError error={new Error('trasig loader')} reset={reset} />)

    await user.click(screen.getByRole('button', { name: 'Försök igen' }))
    expect(reset).toHaveBeenCalledTimes(1)
  })
})
