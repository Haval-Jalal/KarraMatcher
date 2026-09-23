import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import {
  disableTeamPush,
  enableTeamPush,
  isPushSupported,
  isTeamPushEnabled,
  notificationPermission,
} from '@/lib/push'

import { DevicePushToggle } from '../DevicePushToggle'

/**
 * Växeln "Notiser på den här enheten" (`#244`).
 *
 * Push-mekaniken (`@/lib/push`) är testad för sig; här vaktas att gränssnittet säger rätt sak
 * i varje tillstånd — stöds ej, nekat, av och på — och att växlingen anropar rätt sak.
 */

vi.mock('@/lib/push', () => ({
  isPushSupported: vi.fn(() => true),
  notificationPermission: vi.fn(() => 'default'),
  isTeamPushEnabled: vi.fn(() => false),
  enableTeamPush: vi.fn(),
  disableTeamPush: vi.fn(),
}))

const supported = vi.mocked(isPushSupported)
const permission = vi.mocked(notificationPermission)
const teamEnabled = vi.mocked(isTeamPushEnabled)
const enable = vi.mocked(enableTeamPush)
const disable = vi.mocked(disableTeamPush)

beforeEach(() => {
  supported.mockReturnValue(true)
  permission.mockReturnValue('default')
  teamEnabled.mockReturnValue(false)
  enable.mockReset()
  disable.mockReset()
})

afterEach(() => {
  vi.clearAllMocks()
})

describe('DevicePushToggle', () => {
  it('säger ifrån när webbläsaren saknar stöd', () => {
    supported.mockReturnValue(false)

    render(<DevicePushToggle teamSlug="gul" />)

    expect(screen.getByText(/kan inte visa notiser/)).toBeInTheDocument()
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  })

  it('slår på notiser och visar växeln som på', async () => {
    enable.mockResolvedValue('enabled')

    render(<DevicePushToggle teamSlug="gul" />)

    const toggle = screen.getByRole('checkbox', { name: /Notiser på den här enheten/ })
    expect(toggle).not.toBeChecked()

    await userEvent.click(toggle)

    expect(enable).toHaveBeenCalledWith('gul')
    expect(toggle).toBeChecked()
  })

  it('förklarar ett nekat tillstånd i stället för att bara misslyckas', async () => {
    enable.mockResolvedValue('denied')

    render(<DevicePushToggle teamSlug="gul" />)

    await userEvent.click(screen.getByRole('checkbox', { name: /Notiser på den här enheten/ }))

    expect(screen.getByText(/Notiser är blockerade/)).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: /Notiser på den här enheten/ })).not.toBeChecked()
  })

  it('slår av notiser när den redan är på', async () => {
    teamEnabled.mockReturnValue(true)
    disable.mockResolvedValue(true)

    render(<DevicePushToggle teamSlug="gul" />)

    const toggle = screen.getByRole('checkbox', { name: /Notiser på den här enheten/ })
    expect(toggle).toBeChecked()

    await userEvent.click(toggle)

    expect(disable).toHaveBeenCalledWith('gul')
    expect(toggle).not.toBeChecked()
  })

  it('låser växeln och vägleder när notiser är blockerade i webbläsaren', () => {
    permission.mockReturnValue('denied')

    render(<DevicePushToggle teamSlug="gul" />)

    expect(screen.getByRole('checkbox', { name: /Notiser på den här enheten/ })).toBeDisabled()
    expect(screen.getByText(/blockerade i webbläsaren/)).toBeInTheDocument()
  })
})
