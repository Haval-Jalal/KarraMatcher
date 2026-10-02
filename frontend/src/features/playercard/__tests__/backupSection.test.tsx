import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { BackupSection } from '../BackupSection'
import { backupSignature, emptyCard, type PlayerCardData } from '../storage/schema'

/**
 * Säkerhetskopian (§KM.2, `#535`). Kortet finns bara på telefonen, så "kopierad" måste vara sant:
 * en falsk bekräftelse släcker "Ingen kopia än"-varningen och lämnar familjen utan kopia.
 */

function cardWithContent(): PlayerCardData {
  return {
    ...emptyCard(),
    children: [{ id: '1', name: 'Elias', shirtNumber: null, teamSlug: null, seenBadges: [] }],
    lastBackupUtc: null,
  }
}

function setClipboard(writeText: (text: string) => Promise<void>): void {
  Object.defineProperty(navigator, 'clipboard', {
    value: { writeText },
    configurable: true,
  })
}

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('kopiera säkerhetskopieringskoden', () => {
  it('bekräftar och stämplar kopian bara när urklippet verkligen skrevs', async () => {
    setClipboard(vi.fn().mockResolvedValue(undefined))
    const onChanged = vi.fn()

    render(<BackupSection card={cardWithContent()} onChanged={onChanged} />)

    await userEvent.click(screen.getByRole('button', { name: 'Kopiera koden' }))

    expect(await screen.findByText(/Koden är kopierad/)).toBeInTheDocument()
    // Kortet stämplas (lastBackupUtc) via onChanged — bara vid en lyckad skrivning.
    expect(onChanged).toHaveBeenCalledTimes(1)
  })

  it('behåller varningen och ber om manuell kopia när urklippet misslyckas (#535)', async () => {
    setClipboard(vi.fn().mockRejectedValue(new Error('blockerat')))
    const onChanged = vi.fn()

    render(<BackupSection card={cardWithContent()} onChanged={onChanged} />)

    await userEvent.click(screen.getByRole('button', { name: 'Kopiera koden' }))

    // Felet ska synas, och kopian får inte stämplas som gjord.
    expect(await screen.findByText(/kunde inte kopieras automatiskt/)).toBeInTheDocument()
    expect(onChanged).not.toHaveBeenCalled()

    // Ingen falsk "kopierad"-bekräftelse, och "Ingen kopia än"-varningen står kvar.
    expect(screen.queryByText(/Koden är kopierad/)).not.toBeInTheDocument()
    expect(screen.getByText(/Ingen kopia än/)).toBeInTheDocument()

    // "Visa koden" öppnas så koden kan markeras för hand.
    await waitFor(() =>
      expect(
        screen.getByText('Visa koden', { selector: 'summary' }).closest('details'),
      ).toHaveAttribute('open'),
    )
  })
})

describe('säkerhetskopians färskhet (#596)', () => {
  it('visar ett neutralt läge för ett tomt kort, inte "säkerhetskopierad"', () => {
    render(<BackupSection card={emptyCard()} onChanged={vi.fn()} />)

    expect(screen.getByText(/Inget att säkerhetskopiera än/)).toBeInTheDocument()
    expect(screen.queryByText(/Säkerhetskopierad/)).not.toBeInTheDocument()
  })

  it('säger "Säkerhetskopierad" när signaturen matchar innehållet', () => {
    const card = cardWithContent()
    const backed: PlayerCardData = {
      ...card,
      lastBackupUtc: '2026-10-01T10:00:00.000Z',
      lastBackupSignature: backupSignature(card),
    }

    render(<BackupSection card={backed} onChanged={vi.fn()} />)

    expect(screen.getByText(/Säkerhetskopierad/)).toBeInTheDocument()
    expect(screen.queryByText(/Nya resultat sedan din senaste kopia/)).not.toBeInTheDocument()
  })

  it('varnar för nya resultat när innehållet ändrats sedan kopian', () => {
    const card = cardWithContent()
    const changed: PlayerCardData = {
      ...card,
      lastBackupUtc: '2026-10-01T10:00:00.000Z',
      // Signatur från ett annat innehåll → kortet har ändrats sedan dess.
      lastBackupSignature: backupSignature({ ...card, children: [] }),
    }

    render(<BackupSection card={changed} onChanged={vi.fn()} />)

    expect(screen.getByText(/Nya resultat sedan din senaste kopia/)).toBeInTheDocument()
  })

  it('varnar inte falskt för ett äldre kort utan sparad signatur', () => {
    // Ett kort säkerhetskopierat innan signaturen fanns (lastBackupSignature saknas) ska visa
    // "Säkerhetskopierad", inte en falsk "nya resultat"-varning, tills nästa kopia stämplat en.
    const card = cardWithContent()
    const legacy: PlayerCardData = {
      ...card,
      lastBackupUtc: '2026-10-01T10:00:00.000Z',
      lastBackupSignature: null,
    }

    render(<BackupSection card={legacy} onChanged={vi.fn()} />)

    expect(screen.getByText(/Säkerhetskopierad/)).toBeInTheDocument()
    expect(screen.queryByText(/Nya resultat sedan din senaste kopia/)).not.toBeInTheDocument()
  })
})
