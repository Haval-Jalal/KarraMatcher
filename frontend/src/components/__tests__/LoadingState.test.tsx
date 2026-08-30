import { act, render, renderHook, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { LoadingState } from '@/components/LoadingState'
import { SLOW_AFTER_MS, useSlowRequest } from '@/lib/useSlowRequest'
import CSS_SOURCE from '@/styles/index.css?raw'

/**
 * Väntan (`#118`).
 *
 * Två saker vaktas. Att den som lyssnar får ett besked i ord — grå rutor betyder
 * ingenting för en skärmläsare, och en form utan text hade gjort väntan tystare än den
 * var förut. Och att ett långsamt anrop förklarar sig, eftersom Render sover efter en
 * kvart och tar omkring 50 sekunder att vakna (§KM.11). Utan den förklaringen ser appen
 * ut att ha hängt sig, och det är lördag morgon när det händer.
 */

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true })
})

afterEach(() => {
  vi.useRealTimers()
})

describe('väntan säger vad den väntar på', () => {
  it('läser upp vad som hämtas', () => {
    render(<LoadingState label="Hämtar matcherna…" />)

    expect(screen.getByRole('status')).toHaveTextContent('Hämtar matcherna…')
  })

  it('döljer formen för skärmläsaren', () => {
    // Tre grå rutor är brus för den som lyssnar. Beskedet ligger i texten.
    render(
      <LoadingState label="Hämtar matcherna…">
        <div data-testid="form" />
      </LoadingState>,
    )

    expect(screen.getByTestId('form').parentElement).toHaveAttribute('aria-hidden', 'true')
  })

  it('avbryter inte det man håller på med', () => {
    // status och inte alert: väntan är inget man ska ryckas ur en mening för.
    render(<LoadingState label="Hämtar lagen…" />)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})

describe('ett långsamt anrop förklarar sig', () => {
  it('säger ingenting om servern med en gång', () => {
    render(<LoadingState label="Hämtar matcherna…" />)

    expect(screen.queryByText(/Servern har sovit/)).not.toBeInTheDocument()
  })

  it('berättar att servern startar när det dröjer', async () => {
    render(<LoadingState label="Hämtar matcherna…" />)

    await vi.advanceTimersByTimeAsync(SLOW_AFTER_MS + 100)

    expect(await screen.findByText(/Servern har sovit och startar igen/)).toBeInTheDocument()
  })

  it('lägger förklaringen i samma besked, så den läses upp', async () => {
    // Ett nytt element utanför role="status" hade aldrig annonserats.
    render(<LoadingState label="Hämtar matcherna…" />)

    await act(async () => {
      await vi.advanceTimersByTimeAsync(SLOW_AFTER_MS + 100)
    })

    expect(screen.getByRole('status')).toHaveTextContent(/Servern har sovit/)
  })
})

describe('mätningen av att det går långsamt', () => {
  it('slår inte till innan tröskeln', async () => {
    const { result } = renderHook(() => useSlowRequest(true))

    await act(async () => {
      await vi.advanceTimersByTimeAsync(SLOW_AFTER_MS - 100)
    })

    expect(result.current).toBe(false)
  })

  it('nollställs när anropet blir klart', async () => {
    // Annars står förklaringen kvar och påstår att servern sover nästa gång man laddar.
    const { result, rerender } = renderHook(({ pending }) => useSlowRequest(pending), {
      initialProps: { pending: true },
    })

    await act(async () => {
      await vi.advanceTimersByTimeAsync(SLOW_AFTER_MS + 100)
    })
    expect(result.current).toBe(true)

    rerender({ pending: false })
    expect(result.current).toBe(false)
  })

  it('startar ingen klocka för ett anrop som inte pågår', async () => {
    const { result } = renderHook(() => useSlowRequest(false))

    await act(async () => {
      await vi.advanceTimersByTimeAsync(SLOW_AFTER_MS * 4)
    })

    expect(result.current).toBe(false)
  })
})

describe('rörelsen stannar för den som bett om det', () => {
  it('animerar rutorna', () => {
    expect(CSS_SOURCE).toMatch(/\.skeleton\s*\{[^}]*animation:\s*skeleton-sweep/)
  })

  it('nollar alla animationer under prefers-reduced-motion', () => {
    // Regeln träffar `*`, alltså även skelettet. Rutan blir stillastående och fullt
    // synlig — formen bär beskedet, rörelsen är bara en påminnelse.
    const block = CSS_SOURCE.slice(CSS_SOURCE.indexOf('@media (prefers-reduced-motion: reduce)'))

    expect(block).toContain('*')
    expect(block).toMatch(/animation-duration:\s*0\.01ms\s*!important/)
  })
})
