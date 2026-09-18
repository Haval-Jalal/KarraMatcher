import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { EventList } from '@/features/events'
import { testEvent } from '@/test/apiStub'
import { renderWithRouter } from '@/test/renderWithRouter'

const now = '2026-09-15T09:00:00Z'

describe('EventList — innehåll', () => {
  it('säger till när laget saknar matcher', async () => {
    await renderWithRouter(<EventList events={[]} now={now} />)

    expect(screen.getByText(/Inga händelser är inlagda/)).toBeInTheDocument()
  })

  it('förklarar varför listan är tom, inte bara att den är det', async () => {
    // En tom lista utan förklaring läses som ett fel. Den vanligaste orsaken är att
    // säsongens schema inte lagts in än, och det ska stå — annars ringer någon tränaren.
    await renderWithRouter(<EventList events={[]} now={now} />)

    expect(screen.getByText(/Schemat läggs in inför säsongen/)).toBeInTheDocument()
  })

  it('delar upp kommande matcher under månadsrubriker', async () => {
    await renderWithRouter(
      <EventList
        events={[testEvent('a', '2026-09-20T12:00:00Z'), testEvent('b', '2026-10-04T12:00:00Z')]}
        now={now}
      />,
    )

    expect(screen.getByRole('heading', { name: 'September 2026' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Oktober 2026' })).toBeInTheDocument()
  })

  it('framhäver dagens matcher under en egen rubrik', async () => {
    await renderWithRouter(
      <EventList events={[testEvent('a', '2026-09-15T16:00:00Z')]} now={now} />,
    )

    expect(screen.getByRole('heading', { name: 'Idag' })).toBeInTheDocument()
  })

  it('visar avspark i svensk tid', async () => {
    // Testerna körs i America/Los_Angeles. 12:00 UTC är 14:00 i Sverige i september.
    await renderWithRouter(
      <EventList events={[testEvent('a', '2026-09-20T12:00:00Z')]} now={now} />,
    )

    expect(screen.getByText('14:00')).toBeInTheDocument()
  })

  it('säger att säsongen är slut i stället för att visa en tom lista', async () => {
    // Utan det här ser sidan trasig ut i november, när allt ligger bakom historikknappen.
    await renderWithRouter(
      <EventList events={[testEvent('a', '2026-08-15T12:00:00Z')]} now={now} />,
    )

    expect(screen.getByText(/Säsongen är slut/)).toBeInTheDocument()
  })
})

describe('EventList — inställda matcher', () => {
  it('märker inställd match med text, inte bara färg', async () => {
    // WCAG 1.4.1: en förälder som inte skiljer färger ska förstå att matchen är inställd.
    await renderWithRouter(
      <EventList
        events={[testEvent('a', '2026-09-20T12:00:00Z', { status: 'Cancelled' })]}
        now={now}
      />,
    )

    expect(screen.getByText('Inställd')).toBeInTheDocument()
  })

  it('märker framflyttad match', async () => {
    await renderWithRouter(
      <EventList
        events={[testEvent('a', '2026-09-20T12:00:00Z', { status: 'Postponed' })]}
        now={now}
      />,
    )

    expect(screen.getByText('Framflyttad')).toBeInTheDocument()
  })
})

describe('EventList — tidigare matcher', () => {
  const matches = [
    testEvent('gammal', '2026-08-15T12:00:00Z'),
    testEvent('nyare', '2026-09-05T12:00:00Z'),
    testEvent('kommande', '2026-09-20T12:00:00Z'),
  ]

  it('fäller ihop historiken bakom en knapp som anger antalet', async () => {
    await renderWithRouter(<EventList events={matches} now={now} />)

    expect(screen.getByRole('button', { name: 'Visa 2 tidigare händelser' })).toBeInTheDocument()
  })

  it('böjer ordet rätt när det bara är en match', async () => {
    await renderWithRouter(<EventList events={[matches[0]!, matches[2]!]} now={now} />)

    expect(screen.getByRole('button', { name: 'Visa 1 tidigare händelse' })).toBeInTheDocument()
  })

  it('döljer historiken tills den fälls ut', async () => {
    const user = userEvent.setup()
    await renderWithRouter(<EventList events={matches} now={now} />)

    const toggle = screen.getByRole('button', { name: /Visa 2 tidigare/ })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')

    await user.click(toggle)

    expect(toggle).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('button', { name: 'Dölj tidigare händelser' })).toBeInTheDocument()
  })

  it('pekar knappen på det område den styr', async () => {
    // aria-controls kopplar knappen till innehållet för skärmläsare.
    const user = userEvent.setup()
    await renderWithRouter(<EventList events={matches} now={now} />)

    const toggle = screen.getByRole('button', { name: /Visa 2 tidigare/ })
    const controlled = document.getElementById(toggle.getAttribute('aria-controls') ?? '')

    expect(controlled).not.toBeNull()
    expect(controlled).toHaveAttribute('hidden')

    await user.click(toggle)

    expect(controlled).not.toHaveAttribute('hidden')
  })

  it('visar senast spelade match först i historiken', async () => {
    const user = userEvent.setup()
    await renderWithRouter(<EventList events={matches} now={now} />)

    await user.click(screen.getByRole('button', { name: /Visa 2 tidigare/ }))

    const controlled = document.getElementById(
      screen.getByRole('button', { name: /Dölj/ }).getAttribute('aria-controls') ?? '',
    )
    const headings = within(controlled!)
      .getAllByRole('heading')
      .map((heading) => heading.textContent)

    expect(headings).toEqual(['September 2026', 'Augusti 2026'])
  })

  it('visar ingen knapp när det inte finns någon historik', async () => {
    await renderWithRouter(
      <EventList events={[testEvent('a', '2026-09-20T12:00:00Z')]} now={now} />,
    )

    expect(screen.queryByRole('button', { name: /tidigare/ })).not.toBeInTheDocument()
  })
})

describe('EventList — matchen som redan visas i kortet', () => {
  it('utesluter kortets match ur listan', async () => {
    await renderWithRouter(
      <EventList
        events={[
          testEvent('i-kortet', '2026-09-20T12:00:00Z'),
          testEvent('nasta', '2026-09-27T12:00:00Z'),
        ]}
        now={now}
        excludeId="i-kortet"
      />,
    )

    expect(screen.queryByText(/Motstandare i-kortet/)).not.toBeInTheDocument()
    expect(screen.getByText(/Motstandare nasta/)).toBeInTheDocument()
  })

  it('visar fortfarande en inställd match som kortet hoppade över', async () => {
    // Kortet hoppar över inställda matcher, så den som visas där är inte alltid listans
    // första. Att i stället ta bort första posten hade dolt just den inställda matchen —
    // som är den föräldrar behöver se.
    await renderWithRouter(
      <EventList
        events={[
          testEvent('installd', '2026-09-18T12:00:00Z', { status: 'Cancelled' }),
          testEvent('i-kortet', '2026-09-20T12:00:00Z'),
        ]}
        now={now}
        excludeId="i-kortet"
      />,
    )

    expect(screen.getByText(/Motstandare installd/)).toBeInTheDocument()
    expect(screen.getByText('Inställd')).toBeInTheDocument()
    expect(screen.queryByText(/Motstandare i-kortet/)).not.toBeInTheDocument()
  })

  it('renderar ingen tom Idag-rubrik när dagens enda match ligger i kortet', async () => {
    await renderWithRouter(
      <EventList
        events={[
          testEvent('idag', '2026-09-15T16:00:00Z'),
          testEvent('sen', '2026-09-27T12:00:00Z'),
        ]}
        now={now}
        excludeId="idag"
      />,
    )

    expect(screen.queryByRole('heading', { name: 'Idag' })).not.toBeInTheDocument()
  })

  it('säger att inga fler matcher finns när kortets match var den enda', async () => {
    await renderWithRouter(
      <EventList events={[testEvent('enda', '2026-09-20T12:00:00Z')]} now={now} excludeId="enda" />,
    )

    expect(screen.getByText('Inga fler händelser är inlagda.')).toBeInTheDocument()
  })

  it('säger inte att säsongen är slut när ett kort visar en kommande match', async () => {
    // Kortet står kvar ovanför. "Säsongen är slut" hade motsagt det som syns strax ovan.
    await renderWithRouter(
      <EventList
        events={[
          testEvent('spelad', '2026-08-15T12:00:00Z'),
          testEvent('i-kortet', '2026-09-20T12:00:00Z'),
        ]}
        now={now}
        excludeId="i-kortet"
      />,
    )

    expect(screen.queryByText(/Säsongen är slut/)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Visa 1 tidigare händelse/ })).toBeInTheDocument()
  })

  it('visar hela listan när inget kort finns', async () => {
    await renderWithRouter(
      <EventList
        events={[testEvent('a', '2026-09-20T12:00:00Z'), testEvent('b', '2026-09-27T12:00:00Z')]}
        now={now}
      />,
    )

    expect(screen.getByText(/Motstandare a/)).toBeInTheDocument()
    expect(screen.getByText(/Motstandare b/)).toBeInTheDocument()
  })
})
