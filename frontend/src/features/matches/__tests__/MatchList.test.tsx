import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { MatchList } from '@/features/matches'
import { testMatch } from '@/test/apiStub'

const now = '2026-09-15T09:00:00Z'

describe('MatchList — innehåll', () => {
  it('säger till när laget saknar matcher', () => {
    render(<MatchList matches={[]} now={now} />)

    expect(screen.getByText(/Inga matcher är inlagda/)).toBeInTheDocument()
  })

  it('delar upp kommande matcher under månadsrubriker', () => {
    render(
      <MatchList
        matches={[testMatch('a', '2026-09-20T12:00:00Z'), testMatch('b', '2026-10-04T12:00:00Z')]}
        now={now}
      />,
    )

    expect(screen.getByRole('heading', { name: 'September 2026' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Oktober 2026' })).toBeInTheDocument()
  })

  it('framhäver dagens matcher under en egen rubrik', () => {
    render(<MatchList matches={[testMatch('a', '2026-09-15T16:00:00Z')]} now={now} />)

    expect(screen.getByRole('heading', { name: 'Idag' })).toBeInTheDocument()
  })

  it('visar avspark i svensk tid', () => {
    // Testerna körs i America/Los_Angeles. 12:00 UTC är 14:00 i Sverige i september.
    render(<MatchList matches={[testMatch('a', '2026-09-20T12:00:00Z')]} now={now} />)

    expect(screen.getByText('14:00')).toBeInTheDocument()
  })

  it('säger att säsongen är slut i stället för att visa en tom lista', () => {
    // Utan det här ser sidan trasig ut i november, när allt ligger bakom historikknappen.
    render(<MatchList matches={[testMatch('a', '2026-08-15T12:00:00Z')]} now={now} />)

    expect(screen.getByText(/Säsongen är slut/)).toBeInTheDocument()
  })
})

describe('MatchList — inställda matcher', () => {
  it('märker inställd match med text, inte bara färg', () => {
    // WCAG 1.4.1: en förälder som inte skiljer färger ska förstå att matchen är inställd.
    render(
      <MatchList
        matches={[testMatch('a', '2026-09-20T12:00:00Z', { status: 'Cancelled' })]}
        now={now}
      />,
    )

    expect(screen.getByText('Inställd')).toBeInTheDocument()
  })

  it('märker framflyttad match', () => {
    render(
      <MatchList
        matches={[testMatch('a', '2026-09-20T12:00:00Z', { status: 'Postponed' })]}
        now={now}
      />,
    )

    expect(screen.getByText('Framflyttad')).toBeInTheDocument()
  })
})

describe('MatchList — tidigare matcher', () => {
  const matches = [
    testMatch('gammal', '2026-08-15T12:00:00Z'),
    testMatch('nyare', '2026-09-05T12:00:00Z'),
    testMatch('kommande', '2026-09-20T12:00:00Z'),
  ]

  it('fäller ihop historiken bakom en knapp som anger antalet', () => {
    render(<MatchList matches={matches} now={now} />)

    expect(screen.getByRole('button', { name: 'Visa 2 tidigare matcher' })).toBeInTheDocument()
  })

  it('böjer ordet rätt när det bara är en match', () => {
    render(<MatchList matches={[matches[0]!, matches[2]!]} now={now} />)

    expect(screen.getByRole('button', { name: 'Visa 1 tidigare match' })).toBeInTheDocument()
  })

  it('döljer historiken tills den fälls ut', async () => {
    const user = userEvent.setup()
    render(<MatchList matches={matches} now={now} />)

    const toggle = screen.getByRole('button', { name: /Visa 2 tidigare/ })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')

    await user.click(toggle)

    expect(toggle).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('button', { name: 'Dölj tidigare matcher' })).toBeInTheDocument()
  })

  it('pekar knappen på det område den styr', async () => {
    // aria-controls kopplar knappen till innehållet för skärmläsare.
    const user = userEvent.setup()
    render(<MatchList matches={matches} now={now} />)

    const toggle = screen.getByRole('button', { name: /Visa 2 tidigare/ })
    const controlled = document.getElementById(toggle.getAttribute('aria-controls') ?? '')

    expect(controlled).not.toBeNull()
    expect(controlled).toHaveAttribute('hidden')

    await user.click(toggle)

    expect(controlled).not.toHaveAttribute('hidden')
  })

  it('visar senast spelade match först i historiken', async () => {
    const user = userEvent.setup()
    render(<MatchList matches={matches} now={now} />)

    await user.click(screen.getByRole('button', { name: /Visa 2 tidigare/ }))

    const controlled = document.getElementById(
      screen.getByRole('button', { name: /Dölj/ }).getAttribute('aria-controls') ?? '',
    )
    const headings = within(controlled!)
      .getAllByRole('heading')
      .map((heading) => heading.textContent)

    expect(headings).toEqual(['September 2026', 'Augusti 2026'])
  })

  it('visar ingen knapp när det inte finns någon historik', () => {
    render(<MatchList matches={[testMatch('a', '2026-09-20T12:00:00Z')]} now={now} />)

    expect(screen.queryByRole('button', { name: /tidigare/ })).not.toBeInTheDocument()
  })
})
