import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { SlowRequestNotice } from '@/components/SlowRequestNotice'
import { SLOW_AFTER_MS } from '@/lib/useSlowRequest'

/**
 * Kallstarts-beskedet för en skrivning (`#591`, §KM.11).
 *
 * Samma mätning som sid-laddningen (LoadingState) använder, men för en mutation. En skrivning
 * efter en tyst natt kan hänga ~50 s medan Render vaknar; då ska appen säga varför i stället för
 * en knapp som ser frusen ut. Men bara när det faktiskt dröjer — ett normalt anrop säger inget.
 */

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true })
})

afterEach(() => {
  vi.useRealTimers()
})

describe('SlowRequestNotice', () => {
  it('säger ingenting direkt när en skrivning börjar', () => {
    render(<SlowRequestNotice isPending={true} />)

    expect(screen.queryByText(/Servern har sovit/)).not.toBeInTheDocument()
  })

  it('berättar att servern vaknar när skrivningen dröjer', async () => {
    render(<SlowRequestNotice isPending={true} />)

    await vi.advanceTimersByTimeAsync(SLOW_AFTER_MS + 100)

    // role="status": väntan annonseras utan att avbryta (det är inte ett fel).
    expect(await screen.findByRole('status')).toHaveTextContent(
      /Servern har sovit och startar igen/,
    )
  })

  it('säger ingenting för en skrivning som inte pågår', async () => {
    render(<SlowRequestNotice isPending={false} />)

    await vi.advanceTimersByTimeAsync(SLOW_AFTER_MS * 4)

    expect(screen.queryByText(/Servern har sovit/)).not.toBeInTheDocument()
  })
})
