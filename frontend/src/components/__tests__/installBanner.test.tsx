import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { InstallBanner } from '@/components/InstallBanner'
import { isInstalled, isIos } from '@/lib/platform'

vi.mock('@/lib/platform', () => ({
  isIos: vi.fn(),
  isInstalled: vi.fn(),
}))

const mockIsIos = vi.mocked(isIos)
const mockIsInstalled = vi.mocked(isInstalled)

beforeEach(() => {
  localStorage.clear()
  mockIsIos.mockReturnValue(false)
  mockIsInstalled.mockReturnValue(false)
})

afterEach(() => {
  vi.clearAllMocks()
})

describe('installations-banner', () => {
  it('visar iOS-instruktionen på en oinstallerad iPhone', () => {
    mockIsIos.mockReturnValue(true)

    render(<InstallBanner />)

    expect(screen.getByText(/Lägg till på hemskärmen/)).toBeInTheDocument()
    // Safari kan inte visa en installations-knapp — bara instruktionen.
    expect(screen.queryByRole('button', { name: 'Installera' })).not.toBeInTheDocument()
  })

  it('visar inget när appen redan är installerad', () => {
    mockIsIos.mockReturnValue(true)
    mockIsInstalled.mockReturnValue(true)

    const { container } = render(<InstallBanner />)

    expect(container).toBeEmptyDOMElement()
  })

  it('visar inget på en dator utan installations-erbjudande', () => {
    render(<InstallBanner />)

    expect(screen.queryByRole('note')).not.toBeInTheDocument()
  })

  it('erbjuder en Installera-knapp när webbläsaren tillåter det', async () => {
    render(<InstallBanner />)

    const prompt = vi.fn().mockResolvedValue(undefined)
    const event = new Event('beforeinstallprompt')
    ;(event as unknown as { prompt: unknown }).prompt = prompt

    act(() => {
      window.dispatchEvent(event)
    })

    await userEvent.click(await screen.findByRole('button', { name: 'Installera' }))

    expect(prompt).toHaveBeenCalledOnce()
  })

  it('tjatar inte efter att man stängt den', async () => {
    mockIsIos.mockReturnValue(true)

    const first = render(<InstallBanner />)

    await userEvent.click(screen.getByRole('button', { name: 'Stäng' }))
    expect(screen.queryByRole('note')).not.toBeInTheDocument()

    // Ny session: valet är sparat på enheten, så bannern kommer inte tillbaka.
    first.unmount()
    render(<InstallBanner />)
    expect(screen.queryByRole('note')).not.toBeInTheDocument()
  })
})
