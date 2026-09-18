import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { NextEventCard, selectNextEvent } from '@/features/events'
import { testEvent } from '@/test/apiStub'
import { renderWithRouter } from '@/test/renderWithRouter'

const now = '2026-09-15T09:00:00Z'

describe('selectNextEvent — urval', () => {
  it('väljer den närmaste kommande matchen', () => {
    const next = selectNextEvent(
      [testEvent('sen', '2026-10-04T12:00:00Z'), testEvent('snar', '2026-09-20T12:00:00Z')],
      now,
    )

    expect(next?.id).toBe('snar')
  })

  it('hoppar över inställda matcher', () => {
    // Kortet får inte peka på en match som inte blir av.
    const next = selectNextEvent(
      [
        testEvent('installd', '2026-09-20T12:00:00Z', { status: 'Cancelled' }),
        testEvent('spelas', '2026-09-27T12:00:00Z'),
      ],
      now,
    )

    expect(next?.id).toBe('spelas')
  })

  it('hoppar över framflyttade matcher', () => {
    // Postponed betyder flyttad utan nytt datum: tiden som står kvar är den gamla. Att
    // lyfta fram den hade varit att med emfas visa fel tid.
    const next = selectNextEvent(
      [
        testEvent('flyttad', '2026-09-20T12:00:00Z', { status: 'Postponed' }),
        testEvent('spelas', '2026-09-27T12:00:00Z'),
      ],
      now,
    )

    expect(next?.id).toBe('spelas')
  })

  it('behåller dagens match hela dagen', () => {
    // En förälder som öppnar appen på eftermiddagen ska fortfarande se dagens match.
    const next = selectNextEvent(
      [testEvent('idag', '2026-09-15T07:00:00Z')],
      '2026-09-15T20:00:00Z',
    )

    expect(next?.id).toBe('idag')
  })

  it('hoppar över spelade matcher', () => {
    const next = selectNextEvent(
      [testEvent('spelad', '2026-08-15T12:00:00Z'), testEvent('kommande', '2026-09-20T12:00:00Z')],
      now,
    )

    expect(next?.id).toBe('kommande')
  })

  it('ger null när säsongen är slut', () => {
    expect(selectNextEvent([testEvent('spelad', '2026-08-15T12:00:00Z')], now)).toBeNull()
  })

  it('ger null när alla kommande matcher är inställda', () => {
    const next = selectNextEvent(
      [testEvent('a', '2026-09-20T12:00:00Z', { status: 'Cancelled' })],
      now,
    )

    expect(next).toBeNull()
  })

  it('ger null utan matcher alls', () => {
    expect(selectNextEvent([], now)).toBeNull()
  })

  it('litar inte på att listan kommer sorterad', () => {
    // API:t sorterar, men kortet får inte bero på det — en felsorterad lista hade tyst
    // pekat ut fel match.
    const next = selectNextEvent(
      [testEvent('sen', '2026-10-04T12:00:00Z'), testEvent('snar', '2026-09-16T12:00:00Z')],
      now,
    )

    expect(next?.id).toBe('snar')
  })
})

describe('NextEventCard — visning', () => {
  it('visar motståndare, tid och plats', async () => {
    await renderWithRouter(
      <NextEventCard event={testEvent('a', '2026-09-20T12:00:00Z')} now={now} />,
    )

    expect(screen.getByRole('heading', { name: 'Nästa match' })).toBeInTheDocument()
    expect(screen.getByText(/Motstandare a/)).toBeInTheDocument()
    expect(screen.getByText('14:00')).toBeInTheDocument()
    expect(screen.getByText(/Klarebergsvallen/)).toBeInTheDocument()
  })

  it.each([
    ['2026-09-15T16:00:00Z', 'Idag'],
    ['2026-09-16T16:00:00Z', 'Imorgon'],
    ['2026-09-19T16:00:00Z', 'På lördag'],
    ['2026-09-27T16:00:00Z', 'Om 12 dagar'],
  ])('visar relativ dag för %s som %s', async (kickoff, expected) => {
    await renderWithRouter(<NextEventCard event={testEvent('a', kickoff)} now={now} />)

    expect(screen.getByText(expected)).toBeInTheDocument()
  })

  it('skiljer hemma och borta', async () => {
    await renderWithRouter(
      <NextEventCard event={testEvent('a', '2026-09-20T12:00:00Z', { isHome: false })} now={now} />,
    )

    expect(screen.getByText(/Borta mot/)).toBeInTheDocument()
  })

  it('räknar relativ dag över månadsskiftet', async () => {
    // 30 september 22:30 UTC är 1 oktober i svensk tid — alltså imorgon, inte idag.
    await renderWithRouter(
      <NextEventCard event={testEvent('a', '2026-09-30T22:30:00Z')} now={'2026-09-30T09:00:00Z'} />,
    )

    expect(screen.getByText('Imorgon')).toBeInTheDocument()
  })
})
