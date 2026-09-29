import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { ConfirmButton } from '../ConfirmButton'

/**
 * Två-stegs-bekräftelse (`#383`): ett oavsiktligt tryck ska inte utföra en oåterkallelig åtgärd.
 */
describe('ConfirmButton', () => {
  it('utför inte förrän man bekräftat', async () => {
    const onConfirm = vi.fn()
    const user = userEvent.setup()

    render(<ConfirmButton label="Ta bort" onConfirm={onConfirm} />)

    await user.click(screen.getByRole('button', { name: 'Ta bort' }))
    expect(onConfirm).not.toHaveBeenCalled()

    await user.click(screen.getByRole('button', { name: 'Bekräfta' }))
    expect(onConfirm).toHaveBeenCalledTimes(1)
  })

  it('går att avbryta utan att utföra', async () => {
    const onConfirm = vi.fn()
    const user = userEvent.setup()

    render(<ConfirmButton label="Ta bort" onConfirm={onConfirm} />)

    await user.click(screen.getByRole('button', { name: 'Ta bort' }))
    await user.click(screen.getByRole('button', { name: 'Avbryt' }))

    expect(onConfirm).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Ta bort' })).toBeInTheDocument()
  })
})
