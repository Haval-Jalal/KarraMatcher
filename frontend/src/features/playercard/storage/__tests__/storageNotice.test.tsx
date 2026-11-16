import { render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { StorageNotice } from '../StorageNotice'

/**
 * StorageNotice stärker varningen när lagringen inte är beständig (`#614`). Förr kastades
 * `requestPersistentStorage`-svaret och gränssnittet sa aldrig till när beständighet nekats eller
 * saknades — just de lägen där en hel säsong är som mest utsatt (§KM.2).
 */

function stubPersisted(value: boolean | 'saknas'): void {
  if (value === 'saknas') {
    vi.stubGlobal('navigator', {})
    return
  }

  vi.stubGlobal('navigator', {
    storage: { persisted: () => Promise.resolve(value) },
  })
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('StorageNotice', () => {
  it('stärker varningen när lagringen inte är beständig', async () => {
    stubPersisted(false)

    render(<StorageNotice />)

    await waitFor(() =>
      expect(screen.getByText(/har inte lovat att behålla datan/)).toBeInTheDocument(),
    )
  })

  it('nämner det privata fönstret när API:t saknas helt', async () => {
    stubPersisted('saknas')

    render(<StorageNotice />)

    await waitFor(() => expect(screen.getByText(/privat fönster/)).toBeInTheDocument())
  })

  it('visar ingen extra varning när lagringen är beständig', async () => {
    stubPersisted(true)

    render(<StorageNotice />)

    // Grundtexten finns alltid; den stärkta varningen ska inte dyka upp.
    await screen.findByText(/sparas bara på den här telefonen/)
    expect(screen.queryByText(/har inte lovat att behålla datan/)).not.toBeInTheDocument()
  })
})
