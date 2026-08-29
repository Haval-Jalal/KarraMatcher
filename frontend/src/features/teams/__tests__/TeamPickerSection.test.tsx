import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { TeamPickerSection } from '@/features/teams'
import { SELECTED_TEAM_STORAGE_KEY } from '@/features/teams/selectedTeamContext'
import type { Team } from '@/features/teams'
import { renderWithProviders } from '@/test/renderWithProviders'

const teams: Team[] = [
  { slug: 'gul', name: 'Gul', ageGroup: 'P2016', colorHex: '#D9A21B' },
  { slug: 'bla', name: 'Blå', ageGroup: 'P2016', colorHex: '#1E3F8A' },
  { slug: 'vit', name: 'Vit', ageGroup: 'P2016', colorHex: '#D9D9D9' },
  { slug: 'svart', name: 'Svart', ageGroup: 'P2016', colorHex: '#161616' },
]

/** Svarar som API:t gör, utan att gå ut på nätet. */
function mockJson(body: unknown, status = 200) {
  return vi.fn().mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  })
}

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('TeamPickerSection — tillstånd', () => {
  it('visar att lagen hämtas', () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() => new Promise(() => undefined)),
    )

    renderWithProviders(<TeamPickerSection />)

    expect(screen.getByRole('status')).toHaveTextContent('Hämtar lagen')
  })

  it('visar lagen när de kommit', async () => {
    vi.stubGlobal('fetch', mockJson(teams))

    renderWithProviders(<TeamPickerSection />)

    expect(await screen.findByRole('button', { name: /Gul/ })).toBeInTheDocument()
    expect(screen.getAllByRole('button')).toHaveLength(4)
  })

  it('säger till när inga lag finns i stället för att visa tomt', async () => {
    vi.stubGlobal('fetch', mockJson([]))

    renderWithProviders(<TeamPickerSection />)

    // Väntar på texten och inte på rollen: laddningstillståndet har också role="status",
    // så findByRole hade löst ut direkt på fel element.
    expect(await screen.findByText(/Inga lag är upplagda än/)).toBeInTheDocument()
  })

  it('skiljer på uteblivet nät och trasig server', async () => {
    // Appen används på fotbollsplaner med dålig täckning. "Något gick fel" hade varit
    // enklare att skriva och sämre att läsa — det ena går över av sig självt.
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    renderWithProviders(<TeamPickerSection />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Ingen anslutning')
  })

  it('visar serverfel med ProblemDetails-titeln', async () => {
    vi.stubGlobal('fetch', mockJson({ title: 'Något gick fel' }, 500))

    renderWithProviders(<TeamPickerSection />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Kunde inte hämta lagen')
  })

  it('kan försöka igen efter ett fel', async () => {
    const fetchMock = vi
      .fn()
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: () => Promise.resolve(teams),
      })
    vi.stubGlobal('fetch', fetchMock)

    const user = userEvent.setup()
    renderWithProviders(<TeamPickerSection />)

    await user.click(await screen.findByRole('button', { name: 'Försök igen' }))

    expect(await screen.findByRole('button', { name: /Gul/ })).toBeInTheDocument()
  })
})

describe('TeamPickerSection — val och tillgänglighet', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', mockJson(teams))
  })

  it('grupperar knapparna med ett läsbart namn', async () => {
    renderWithProviders(<TeamPickerSection />)

    expect(await screen.findByRole('group', { name: 'Välj lag' })).toBeInTheDocument()
  })

  it('speglar valt lag med aria-pressed', async () => {
    const user = userEvent.setup()
    renderWithProviders(<TeamPickerSection />)

    const gul = await screen.findByRole('button', { name: /Gul/ })
    expect(gul).toHaveAttribute('aria-pressed', 'false')

    await user.click(gul)

    expect(gul).toHaveAttribute('aria-pressed', 'true')
  })

  it('har bara ett lag valt åt gången', async () => {
    const user = userEvent.setup()
    renderWithProviders(<TeamPickerSection />)

    await user.click(await screen.findByRole('button', { name: /Gul/ }))
    await user.click(screen.getByRole('button', { name: /Blå/ }))

    expect(screen.getByRole('button', { name: /Gul/ })).toHaveAttribute('aria-pressed', 'false')
    expect(screen.getByRole('button', { name: /Blå/ })).toHaveAttribute('aria-pressed', 'true')
  })

  it('går att välja lag med enbart tangentbord', async () => {
    // Knapparna måste gå att nå med tabb och aktiveras med mellanslag eller enter.
    const user = userEvent.setup()
    renderWithProviders(<TeamPickerSection />)

    const gul = await screen.findByRole('button', { name: /Gul/ })
    await user.tab()

    expect(gul).toHaveFocus()

    await user.keyboard(' ')

    expect(gul).toHaveAttribute('aria-pressed', 'true')
  })

  it('markerar valet med mer än färg', async () => {
    // WCAG 1.4.1: färg får inte vara det enda som bär betydelsen. Bocken är den synliga
    // signalen vid sidan av ramen och aria-pressed.
    const user = userEvent.setup()
    renderWithProviders(<TeamPickerSection />)

    const gul = await screen.findByRole('button', { name: /Gul/ })
    await user.click(gul)

    expect(gul).toHaveTextContent('✓')
    expect(screen.getByRole('button', { name: /Blå/ })).not.toHaveTextContent('✓')
  })
})

describe('TeamPickerSection — minnet mellan besök', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', mockJson(teams))
  })

  it('sparar valet så att det överlever en omladdning', async () => {
    const user = userEvent.setup()
    const { unmount } = renderWithProviders(<TeamPickerSection />)

    await user.click(await screen.findByRole('button', { name: /Blå/ }))

    await waitFor(() => {
      expect(localStorage.getItem(SELECTED_TEAM_STORAGE_KEY)).toBe('bla')
    })

    // Att montera om är så nära en omladdning vi kommer: allt tillstånd i minnet är borta,
    // bara lagringen finns kvar.
    unmount()
    renderWithProviders(<TeamPickerSection />)

    expect(await screen.findByRole('button', { name: /Blå/ })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })

  it('sparar lagets slug och inte dess plats i listan', async () => {
    // Ett index hade pekat på fel lag så snart ett lag läggs till eller tas bort.
    const user = userEvent.setup()
    renderWithProviders(<TeamPickerSection />)

    await user.click(await screen.findByRole('button', { name: /Svart/ }))

    await waitFor(() => {
      expect(localStorage.getItem(SELECTED_TEAM_STORAGE_KEY)).toBe('svart')
    })
  })

  it('fungerar även när lagring är avstängd', async () => {
    // localStorage kastar i privat läge i vissa webbläsare. Ett tappat lagval är en
    // olägenhet; en vit skärm är ett fel.
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('QuotaExceededError')
    })

    const user = userEvent.setup()
    renderWithProviders(<TeamPickerSection />)

    const gul = await screen.findByRole('button', { name: /Gul/ })
    await user.click(gul)

    expect(gul).toHaveAttribute('aria-pressed', 'true')
  })
})
