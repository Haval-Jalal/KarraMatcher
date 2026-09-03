import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { writeCard } from '@/features/playercard'
import { emptyCard, type Child } from '@/features/playercard/storage/schema'
import { isInstalled, isIos } from '@/lib/platform'
import { stubApi } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Ärligheten om lagringen, och tipset om hemskärmen (`#48`, §KM.2, §KM.9).
 *
 * <para>
 * iOS rensar lagring för webbplatser som inte besökts på sju dagar; appar på hemskärmen är
 * undantagna. Appen används säsongsvis, med ett långt sommaruppehåll. En förälder med bara
 * ett bokmärke kan alltså förlora en hel säsong utan att ha gjort något fel — och det är
 * det de här testerna finns för att förhindra.
 * </para>
 */

const IPHONE =
  'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1'
const ANDROID =
  'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126 Mobile Safari/537.36'
const IPAD_AS_MAC =
  'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15'

function child(id: string, name: string): Child {
  return { id, name, shirtNumber: null, teamSlug: null, seenBadges: [] }
}

/**
 * Låtsas att appen körs på en viss enhet.
 *
 * <para>
 * `matchMedia` stubbas alltid: jsdom saknar den, och utan stubben hade
 * <c>isInstalled()</c> svarat falskt av fel skäl — testet hade då gått grönt utan att
 * kontrollera något.
 * </para>
 */
function onDevice({
  agent,
  touchPoints = 5,
  installed = false,
}: {
  agent: string
  touchPoints?: number
  installed?: boolean
}) {
  vi.stubGlobal('navigator', {
    ...globalThis.navigator,
    userAgent: agent,
    maxTouchPoints: touchPoints,
    clipboard: { writeText: () => Promise.resolve() },
  })

  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: installed && query.includes('standalone'),
    media: query,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
  }))
}

function openCardPage({ withContent = true }: { withContent?: boolean } = {}) {
  writeCard({ ...emptyCard(), children: withContent ? [child('1', 'Elias')] : [] })
  stubApi({})

  return renderRoute('/spelarkort')
}

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('texten om var kortet finns', () => {
  it('står framme, inte bakom en ruta man klickar bort', async () => {
    onDevice({ agent: ANDROID })
    openCardPage()

    expect(await screen.findByText(/Allt här sparas bara på den här telefonen/)).toBeInTheDocument()
  })

  it('säger vad som får statistiken att försvinna', async () => {
    onDevice({ agent: ANDROID })
    openCardPage()

    const user = userEvent.setup()

    await user.click(await screen.findByText('Vad betyder det?'))

    expect(screen.getByText('byter telefon')).toBeInTheDocument()
    expect(screen.getByText('rensar webbläsarens data')).toBeInTheDocument()
    expect(
      screen.getByText(/Säkerhetskopian längre ner är det som skyddar dig/),
    ).toBeInTheDocument()
  })

  it('behåller rådet om hemskärmen även för den som stängt tipset', async () => {
    /*
     * Den som stangde tipset i varas ska kunna hitta skalet igen till hosten -- och det ar
     * just over sommaruppehallet som iOS hinner rensa.
     */
    localStorage.setItem('karra.hemskarmstips', 'ja')
    onDevice({ agent: IPHONE })
    openCardPage()

    const user = userEvent.setup()

    await user.click(await screen.findByText('Vad betyder det?'))

    expect(screen.getByText(/Hemskärmen skyddar datan/)).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Lägg appen på hemskärmen' }),
    ).not.toBeInTheDocument()
  })
})

describe('tipset om hemskärmen', () => {
  it('visas på iPhone', async () => {
    onDevice({ agent: IPHONE })
    openCardPage()

    expect(
      await screen.findByRole('heading', { name: 'Lägg appen på hemskärmen' }),
    ).toBeInTheDocument()
  })

  it('visas på iPad, som utger sig för att vara en Mac', async () => {
    // iPadOS 13 och senare uppger sig vara en Mac. Utan pekskarmskontrollen hade en iPad
    // -- en helt vanlig enhet att lasa schemat pa -- gatt miste om tipset.
    onDevice({ agent: IPAD_AS_MAC, touchPoints: 5 })
    openCardPage()

    expect(
      await screen.findByRole('heading', { name: 'Lägg appen på hemskärmen' }),
    ).toBeInTheDocument()
  })

  it('visas inte på en riktig Mac', async () => {
    onDevice({ agent: IPAD_AS_MAC, touchPoints: 0 })
    openCardPage()

    await screen.findByRole('heading', { level: 1 })

    expect(
      screen.queryByRole('heading', { name: 'Lägg appen på hemskärmen' }),
    ).not.toBeInTheDocument()
  })

  it('visas inte på Android', async () => {
    onDevice({ agent: ANDROID })
    openCardPage()

    await screen.findByRole('heading', { level: 1 })

    expect(
      screen.queryByRole('heading', { name: 'Lägg appen på hemskärmen' }),
    ).not.toBeInTheDocument()
  })

  it('visas inte när appen redan ligger på hemskärmen', async () => {
    // Att tjata pa nagon som redan gjort det ar det snabbaste sattet att lara folk att
    // ignorera appens texter.
    onDevice({ agent: IPHONE, installed: true })
    openCardPage()

    await screen.findByRole('heading', { level: 1 })

    expect(
      screen.queryByRole('heading', { name: 'Lägg appen på hemskärmen' }),
    ).not.toBeInTheDocument()
  })

  it('visas inte innan kortet har något att förlora', async () => {
    /*
     * Samma resonemang som bakom persist()-fragan: den som oppnat appen for att se en
     * matchtid har ingenting att forlora annu, och ett rad om nagot man inte gor ar precis
     * det som lar folk sluta lasa appens texter.
     */
    onDevice({ agent: IPHONE })
    openCardPage({ withContent: false })

    await screen.findByRole('heading', { level: 1 })

    expect(
      screen.queryByRole('heading', { name: 'Lägg appen på hemskärmen' }),
    ).not.toBeInTheDocument()
  })

  it('säger varför installationen bevarar datan', async () => {
    // Utan sambandet ar det ett tips om en genvag. Med det ar det skalet att gora det
    // innan sommaruppehallet.
    onDevice({ agent: IPHONE })
    openCardPage()

    const heading = await screen.findByRole('heading', { name: 'Lägg appen på hemskärmen' })

    /*
     * Fragan begransas till sjalva rutan. Samma samband star permanent i texten ovanfor,
     * och den ligger kvar i DOM:en aven hopfalld -- en osokt fraga hade traffat bada och
     * inte visat att rutan sager det.
     */
    const tip = within(heading.closest('section')!)

    expect(tip.getByText(/inte använts på en vecka/)).toBeInTheDocument()
    expect(tip.getByText(/Appar på hemskärmen slipper det/)).toBeInTheDocument()
  })

  it('kommer inte tillbaka när det stängts', async () => {
    onDevice({ agent: IPHONE })
    const view = openCardPage()

    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'Tack, jag vet' }))

    expect(
      screen.queryByRole('heading', { name: 'Lägg appen på hemskärmen' }),
    ).not.toBeInTheDocument()

    view.unmount()
    openCardPage()

    await screen.findByRole('heading', { level: 1 })

    expect(
      screen.queryByRole('heading', { name: 'Lägg appen på hemskärmen' }),
    ).not.toBeInTheDocument()
  })
})

describe('enhetskontrollen', () => {
  it.each([
    [IPHONE, 5, true],
    [IPAD_AS_MAC, 5, true],
    [IPAD_AS_MAC, 0, false],
    [ANDROID, 5, false],
  ])('%s med %i pekpunkter', (agent, touchPoints, expected) => {
    onDevice({ agent, touchPoints })

    expect(isIos()).toBe(expected)
  })

  it('känner igen en installerad app', () => {
    onDevice({ agent: IPHONE, installed: true })

    expect(isInstalled()).toBe(true)
  })

  it('svarar nej när matchMedia saknas i stället för att kasta', () => {
    /*
     * En sida som kraschar for att en webblasare saknar matchMedia ar samre an ett tips
     * som uteblir.
     */
    vi.stubGlobal('navigator', { userAgent: IPHONE, maxTouchPoints: 5 })
    vi.stubGlobal('matchMedia', undefined)

    expect(() => isInstalled()).not.toThrow()
    expect(isInstalled()).toBe(false)
  })
})
