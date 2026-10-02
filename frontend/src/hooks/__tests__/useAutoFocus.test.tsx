import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { useAutoFocus } from '../useAutoFocus'

/**
 * Fokus-på-mount-hooken (#598). När en panel fälls in i stället för en knapp ska fokus följa med
 * dit, inte falla till <body> (WCAG 2.4.3).
 */

function Probe({ active }: { active: boolean }) {
  const ref = useAutoFocus<HTMLButtonElement>(active)

  return (
    <button type="button" ref={ref}>
      Mål
    </button>
  )
}

describe('useAutoFocus', () => {
  it('fokuserar elementet när det monteras aktivt', () => {
    render(<Probe active />)

    expect(screen.getByRole('button', { name: 'Mål' })).toHaveFocus()
  })

  it('fokuserar inte när inaktiv', () => {
    render(<Probe active={false} />)

    expect(screen.getByRole('button', { name: 'Mål' })).not.toHaveFocus()
  })
})
