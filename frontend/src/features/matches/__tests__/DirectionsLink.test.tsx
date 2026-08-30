import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { DirectionsLink } from '@/features/matches/DirectionsLink'
import { renderWithRouter } from '@/test/renderWithRouter'

afterEach(() => {
  vi.unstubAllGlobals()
})

function stubUserAgent(agent: string) {
  vi.stubGlobal('navigator', { ...globalThis.navigator, userAgent: agent })
}

describe('DirectionsLink', () => {
  it('öppnar Apple Maps på iPhone', async () => {
    stubUserAgent('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)')

    await renderWithRouter(
      <DirectionsLink venueName="Kareby Hed 11" address="Kareby Hed, Kungälv" />,
    )

    expect(screen.getByRole('link', { name: /Vägbeskrivning/ })).toHaveAttribute(
      'href',
      'https://maps.apple.com/?daddr=Kareby%20Hed%2C%20Kung%C3%A4lv&dirflg=d',
    )
  })

  it('öppnar Google Maps på Android', async () => {
    stubUserAgent('Mozilla/5.0 (Linux; Android 14; Pixel 8)')

    await renderWithRouter(
      <DirectionsLink venueName="Kareby Hed 11" address="Kareby Hed, Kungälv" />,
    )

    expect(screen.getByRole('link', { name: /Vägbeskrivning/ })).toHaveAttribute(
      'href',
      'https://www.google.com/maps/dir/?api=1&destination=Kareby%20Hed%2C%20Kung%C3%A4lv&travelmode=driving',
    )
  })

  it('har rel="noopener noreferrer"', async () => {
    // Säkerhetschecklistan 5.6. Utan noopener får den öppnade sidan en referens tillbaka
    // till vårt fönster och kan styra om det.
    await renderWithRouter(<DirectionsLink venueName="Kareby Hed 11" address="Kareby Hed" />)

    const link = screen.getByRole('link', { name: /Vägbeskrivning/ })
    expect(link).toHaveAttribute('rel', 'noopener noreferrer')
    expect(link).toHaveAttribute('target', '_blank')
  })

  it('säger vart länken går och att den lämnar appen', async () => {
    // "Vägbeskrivning" ensamt säger inte vart. Skärmläsaren får hela meningen.
    await renderWithRouter(
      <DirectionsLink venueName="Kareby Hed 11" address="Kareby Hed, Kungälv" />,
    )

    expect(
      screen.getByRole('link', { name: /Kareby Hed, Kungälv, öppnas i kartappen/ }),
    ).toBeInTheDocument()
  })

  it('använder spelplatsens namn när adress saknas', async () => {
    await renderWithRouter(<DirectionsLink venueName="Kareby Hed 11" address={null} />)

    const href = screen.getByRole('link', { name: /Vägbeskrivning/ }).getAttribute('href')

    expect(href).toContain(encodeURIComponent('Kareby Hed 11'))
  })
})
