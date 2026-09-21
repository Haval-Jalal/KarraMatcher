import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { jsonResponse } from '@/test/apiStub'

import { DataExportSection } from '../DataExportSection'
import type { AccountExport } from '../dataExportApi'

/**
 * Registerutdraget så en förälder ser det (`#67`).
 *
 * <para>
 * Vaktar att utdraget faktiskt visas läsbart på svenska, att spelarkortet nämns som
 * frånvarande, och att prenumerationens tekniska adress aldrig syns — den ska varken komma
 * från servern eller renderas här.
 * </para>
 */

const PUSH_ENDPOINT = 'https://fcm.example.com/hemlig-endpoint-abc123'

const EXPORT: AccountExport = {
  exportedUtc: '2026-10-03T09:00:00Z',
  account: {
    email: 'foralder@example.com',
    firstName: 'Anna',
    lastName: 'Berg',
    createdUtc: '2025-10-01T09:00:00Z',
    lastSignedInUtc: '2026-10-02T09:00:00Z',
  },
  carpoolOffers: [
    {
      matchOpponent: 'Torslanda IK',
      matchKickoffUtc: '2026-10-25T11:00:00Z',
      direction: 'Both',
      departurePlace: 'Kärra centrum',
      departureUtc: '2026-10-25T10:00:00Z',
      seats: 3,
      note: 'Har plats för en till',
      status: 'Open',
      createdUtc: '2026-10-23T09:00:00Z',
    },
  ],
  carpoolRequests: [],
  attendanceResponses: [],
  notificationSettings: [
    {
      teamName: 'Gul',
      eventChanges: true,
      kallelser: true,
      carpool: false,
      chat: true,
      updatedUtc: '2026-10-01T09:00:00Z',
    },
  ],
  pushSubscriptions: [{ teamName: 'Gul', createdUtc: '2026-10-01T09:00:00Z', lastUsedUtc: null }],
  playerCard: {
    message:
      'Spelarkortet finns inte i det här utdraget. Det lagras bara i din egen telefon. ' +
      'Använd säkerhetskopieringskoden i appen.',
  },
}

afterEach(() => {
  vi.unstubAllGlobals()
})

function stubExport(body: AccountExport | 'error') {
  vi.stubGlobal(
    'fetch',
    vi.fn(() =>
      body === 'error'
        ? Promise.reject(new TypeError('Failed to fetch'))
        : Promise.resolve(jsonResponse(body)),
    ),
  )
}

describe('DataExportSection', () => {
  it('visar en hämta-knapp innan något laddats', () => {
    stubExport(EXPORT)
    render(<DataExportSection />)

    expect(screen.getByRole('button', { name: 'Hämta mina uppgifter' })).toBeInTheDocument()
    expect(screen.queryByText('foralder@example.com')).not.toBeInTheDocument()
  })

  it('visar kontots uppgifter läsbart efter hämtning', async () => {
    stubExport(EXPORT)
    render(<DataExportSection />)

    await userEvent.click(screen.getByRole('button', { name: 'Hämta mina uppgifter' }))

    await waitFor(() => {
      expect(screen.getByText(/foralder@example.com/)).toBeInTheDocument()
    })
    expect(screen.getByText(/Torslanda IK/)).toBeInTheDocument()
    expect(screen.getByText(/Kärra centrum/)).toBeInTheDocument()
  })

  it('nämner spelarkortet som frånvarande', async () => {
    stubExport(EXPORT)
    render(<DataExportSection />)

    await userEvent.click(screen.getByRole('button', { name: 'Hämta mina uppgifter' }))

    await waitFor(() => {
      expect(screen.getByText(/säkerhetskopieringskoden/i)).toBeInTheDocument()
    })
  })

  it('visar aldrig den tekniska push-adressen', async () => {
    // §KM.10: adressen är en secret. Den kommer inte från servern och renderas inte här.
    stubExport(EXPORT)
    render(<DataExportSection />)

    await userEvent.click(screen.getByRole('button', { name: 'Hämta mina uppgifter' }))

    await waitFor(() => {
      expect(screen.getByText('Gul — sedan 1 oktober 2026.', { exact: false })).toBeInTheDocument()
    })
    expect(screen.queryByText(new RegExp(PUSH_ENDPOINT))).not.toBeInTheDocument()
  })

  it('erbjuder nedladdning som fil efter hämtning', async () => {
    stubExport(EXPORT)
    const createUrl = vi.fn(() => 'blob:mock')
    vi.stubGlobal('URL', { ...URL, createObjectURL: createUrl, revokeObjectURL: vi.fn() })

    render(<DataExportSection />)

    await userEvent.click(screen.getByRole('button', { name: 'Hämta mina uppgifter' }))

    const download = await screen.findByRole('button', { name: 'Ladda ner som fil' })
    await userEvent.click(download)

    expect(createUrl).toHaveBeenCalledTimes(1)
  })

  it('visar ett svenskt felmeddelande om hämtningen misslyckas', async () => {
    stubExport('error')
    render(<DataExportSection />)

    await userEvent.click(screen.getByRole('button', { name: 'Hämta mina uppgifter' }))

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument()
    })
  })
})
