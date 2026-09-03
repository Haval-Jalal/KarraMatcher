import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { readCard, writeCard } from '@/features/playercard'
import { emptyCard } from '@/features/playercard/storage/schema'
import { stubApi } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Barnen på spelarkortet (`#43`, §KM.2).
 *
 * <para>
 * Ska gå på tio sekunder vid köksbordet: ingen inloggning, ingen server, och ingen
 * validering som dömer om vad ett namn är. Den som skriver "Lillen" ska inte mötas av ett
 * felmeddelande.
 * </para>
 */

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('lägga till barn', () => {
  it('sparar barnet på enheten', async () => {
    stubApi({})

    const user = userEvent.setup()
    renderRoute('/spelarkort')

    await user.type(await screen.findByLabelText('Namn eller smeknamn'), 'Elias')
    await user.click(screen.getByRole('button', { name: 'Lägg till' }))

    expect(readCard().children[0]?.name).toBe('Elias')
  })

  it.each(['lillen', 'E', 'Elias 2', 'Nalle-Puh', 'Lillasyster ❤'])(
    'godtar smeknamnet %s',
    async (name) => {
      /*
       * Ingen validering som kraver ett "riktigt" namn.
       *
       * Namnen har ar valda for att falla en naiv kontroll: gemen begynnelsebokstav, en
       * enda bokstav, en siffra, ett bindestreck, en emoji. Forsta versionen av det har
       * testet anvande "Lillen", som ser ut precis som ett formellt fornamn -- det
       * passerade alltsa aven nar jag med flit lade in en namnvalidering, och vaktade
       * darmed ingenting.
       */
      stubApi({})

      const user = userEvent.setup()
      renderRoute('/spelarkort')

      await user.type(await screen.findByLabelText('Namn eller smeknamn'), name)
      await user.click(screen.getByRole('button', { name: 'Lägg till' }))

      expect(screen.queryByRole('alert')).not.toBeInTheDocument()
      expect(readCard().children[0]?.name).toBe(name)
    },
  )

  it('kräver att fältet inte är tomt', async () => {
    // Kravet skiljer "inget ifyllt" från ett namn — det dömer inte om namnet.
    stubApi({})

    const user = userEvent.setup()
    renderRoute('/spelarkort')

    await user.click(await screen.findByRole('button', { name: 'Lägg till' }))

    expect(await screen.findByText('Skriv vad barnet ska heta i appen.')).toBeInTheDocument()
  })

  it('klarar syskon i olika lag', async () => {
    stubApi({})
    writeCard({
      ...emptyCard(),
      children: [
        { id: '1', name: 'Elias', shirtNumber: '7', teamSlug: 'gul', seenBadges: [] },
        { id: '2', name: 'Vera', shirtNumber: null, teamSlug: 'bla', seenBadges: [] },
      ],
    })

    renderRoute('/spelarkort')

    expect(await screen.findByText('Elias')).toBeInTheDocument()
    expect(screen.getByText('Vera')).toBeInTheDocument()
  })
})

describe('ta bort barn', () => {
  it('kräver bekräftelse', async () => {
    stubApi({})
    writeCard({
      ...emptyCard(),
      children: [{ id: '1', name: 'Elias', shirtNumber: null, teamSlug: null, seenBadges: [] }],
    })

    const user = userEvent.setup()
    renderRoute('/spelarkort')

    await user.click(await screen.findByRole('button', { name: 'Ta bort Elias' }))

    expect(readCard().children).toHaveLength(1)
    expect(await screen.findByRole('button', { name: 'Ja, ta bort Elias' })).toBeInTheDocument()
  })

  it('tar med sig barnets matchrapporter', async () => {
    /*
     * En rapport utan barn syns ingenstans men ligger kvar i lagringen -- och foljer med
     * till nasta telefon nar familjen exporterar sin sakerhetskopia. Att radera ska betyda
     * radera (§KM.6).
     */
    stubApi({})
    writeCard({
      ...emptyCard(),
      children: [
        { id: '1', name: 'Elias', shirtNumber: null, teamSlug: null, seenBadges: [] },
        { id: '2', name: 'Vera', shirtNumber: null, teamSlug: null, seenBadges: [] },
      ],
      reports: [
        {
          id: 'r1',
          childId: '1',
          matchId: null,
          playedUtc: '2026-09-20T12:00:00.000Z',
          goals: 1,
          assists: 0,
          teamGoals: null,
          opponentGoals: null,
          opponent: null,
          note: null,
        },
        {
          id: 'r2',
          childId: '2',
          matchId: null,
          playedUtc: '2026-09-20T12:00:00.000Z',
          goals: 2,
          assists: 0,
          teamGoals: null,
          opponentGoals: null,
          opponent: null,
          note: null,
        },
      ],
    })

    const user = userEvent.setup()
    renderRoute('/spelarkort')

    await user.click(await screen.findByRole('button', { name: 'Ta bort Elias' }))
    await user.click(await screen.findByRole('button', { name: 'Ja, ta bort Elias' }))

    const card = readCard()

    expect(card.children).toHaveLength(1)
    expect(card.reports).toHaveLength(1)
    expect(card.reports[0]?.childId).toBe('2')
  })

  it('säger att rapporterna följer med, innan man trycker', async () => {
    stubApi({})
    writeCard({
      ...emptyCard(),
      children: [{ id: '1', name: 'Elias', shirtNumber: null, teamSlug: null, seenBadges: [] }],
    })

    const user = userEvent.setup()
    renderRoute('/spelarkort')

    await user.click(await screen.findByRole('button', { name: 'Ta bort Elias' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/matchrapporter tas bort med/)
  })
})

describe('ändra barn', () => {
  it('sparar ändringen', async () => {
    stubApi({})
    writeCard({
      ...emptyCard(),
      children: [{ id: '1', name: 'Elias', shirtNumber: null, teamSlug: null, seenBadges: [] }],
    })

    const user = userEvent.setup()
    renderRoute('/spelarkort')

    await user.click(await screen.findByRole('button', { name: 'Ändra Elias' }))

    const field = await screen.findByLabelText('Namn eller smeknamn')
    await user.clear(field)
    await user.type(field, 'Lillen')
    await user.click(screen.getByRole('button', { name: 'Spara' }))

    expect(readCard().children[0]?.name).toBe('Lillen')
    expect(readCard().children).toHaveLength(1)
  })
})

describe('texten är ärlig om var datan finns', () => {
  it('säger att ingenting skickas, och vad det innebär', async () => {
    // Styrkan och risken i samma andetag: inget konto behövs, men statistiken följer
    // med telefonen (§KM.2).
    stubApi({})

    renderRoute('/spelarkort')

    expect(await screen.findByText(/Ingenting av det här skickas någonstans/)).toBeInTheDocument()
    expect(screen.getByText(/Byter du telefon behöver du en säkerhetskopia/)).toBeInTheDocument()
  })
})
