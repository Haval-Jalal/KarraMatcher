import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { ScheduleImport } from '@/features/admin'
import { clearSession, setAccessToken } from '@/lib/session'

/**
 * Massinläggets granskning (`#39`).
 *
 * <para>
 * Ingen ska behöva lita på en parser i blindo. Testerna vaktar det som gör funktionen
 * trygg: att ingenting sparas förrän tränaren tryckt på det, och att varje rad får ett
 * besked i ord — inte en färg.
 * </para>
 */

function jsonResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as unknown as Response
}

const preview = {
  imported: 0,
  lines: [
    { lineNumber: 1, rawText: 'Datum\tTid\tLag', outcome: 'Skipped', problem: null },
    { lineNumber: 2, rawText: '2026-09-05\t15:30\t…', outcome: 'Ok', problem: null },
    {
      lineNumber: 3,
      rawText: 'trasig rad',
      outcome: 'Incomplete',
      problem: 'Raden har 1 fält, men en match behöver 5.',
    },
    { lineNumber: 4, rawText: '…', outcome: 'OtherTeam', problem: 'Raden gäller ett annat lag.' },
  ],
}

function stubApi() {
  const calls: string[] = []

  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown, init?: RequestInit) => {
      const url = String(input)
      calls.push(`${init?.method ?? 'GET'} ${url}`)

      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.endsWith('/import/preview')) return Promise.resolve(jsonResponse(preview))
      if (url.endsWith('/import')) {
        return Promise.resolve(jsonResponse({ ...preview, imported: 1 }))
      }

      return Promise.resolve(jsonResponse({}))
    }),
  )

  return calls
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  setAccessToken('en.token')
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('inget sparas förrän tränaren godkänner', () => {
  it('granskar utan att importera', async () => {
    const calls = stubApi()
    const user = userEvent.setup()

    render(<ScheduleImport slug="gul" onImported={vi.fn()} />)

    await user.type(screen.getByLabelText('Inklistrat schema'), 'något')
    await user.click(screen.getByRole('button', { name: 'Granska' }))

    await screen.findByText('Rad för rad')

    expect(calls.some((call) => call.endsWith('/import/preview'))).toBe(true)
    expect(calls.some((call) => call.endsWith('/matches/import'))).toBe(false)
  })

  it('visar knappen för import först efter granskningen', async () => {
    stubApi()
    const user = userEvent.setup()

    render(<ScheduleImport slug="gul" onImported={vi.fn()} />)

    await user.type(screen.getByLabelText('Inklistrat schema'), 'något')

    expect(screen.queryByRole('button', { name: /Lägg till/ })).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Granska' }))

    expect(await screen.findByRole('button', { name: 'Lägg till 1 match' })).toBeInTheDocument()
  })

  it('importerar först när knappen trycks', async () => {
    const calls = stubApi()
    const user = userEvent.setup()

    render(<ScheduleImport slug="gul" onImported={vi.fn()} />)

    await user.type(screen.getByLabelText('Inklistrat schema'), 'något')
    await user.click(screen.getByRole('button', { name: 'Granska' }))
    await user.click(await screen.findByRole('button', { name: 'Lägg till 1 match' }))

    await waitForImport(calls)
  })
})

describe('varje rad får ett besked i ord', () => {
  it('säger vad som händer med varje rad', async () => {
    // Ordet bär beskedet, inte färgen (WCAG 1.4.1) — och "hoppas över" säger vad som
    // händer, till skillnad från "ofullständig" som bara är ett omdöme.
    stubApi()
    const user = userEvent.setup()

    render(<ScheduleImport slug="gul" onImported={vi.fn()} />)

    await user.type(screen.getByLabelText('Inklistrat schema'), 'något')
    await user.click(screen.getByRole('button', { name: 'Granska' }))

    expect(await screen.findByText(/Läggs till/)).toBeInTheDocument()
    expect(screen.getByText(/saknar uppgifter/)).toBeInTheDocument()
    expect(screen.getByText(/gäller ett annat lag/)).toBeInTheDocument()
  })

  it('visar den inklistrade raden så tränaren känner igen den', async () => {
    stubApi()
    const user = userEvent.setup()

    render(<ScheduleImport slug="gul" onImported={vi.fn()} />)

    await user.type(screen.getByLabelText('Inklistrat schema'), 'något')
    await user.click(screen.getByRole('button', { name: 'Granska' }))

    expect(await screen.findByText('trasig rad')).toBeInTheDocument()
  })
})

describe('delvis import', () => {
  it('erbjuder import trots att rader hoppas över', async () => {
    // En trasig rad hindrar inte de som är rätt. Tränaren rättar dem för hand.
    stubApi()
    const user = userEvent.setup()

    render(<ScheduleImport slug="gul" onImported={vi.fn()} />)

    await user.type(screen.getByLabelText('Inklistrat schema'), 'något')
    await user.click(screen.getByRole('button', { name: 'Granska' }))

    expect(await screen.findByRole('button', { name: 'Lägg till 1 match' })).toBeInTheDocument()
    expect(screen.getByText(/saknar uppgifter/)).toBeInTheDocument()
  })
})

async function waitForImport(calls: string[]) {
  const { waitFor } = await import('@testing-library/react')

  await waitFor(() => {
    expect(calls.some((call) => call.endsWith('/matches/import'))).toBe(true)
  })
}
