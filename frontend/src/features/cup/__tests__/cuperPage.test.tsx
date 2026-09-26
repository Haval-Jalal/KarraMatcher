import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { clearSession, setAccessToken } from '@/lib/session'
import { jsonResponse } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

/**
 * Truppens cup-sida (`#304`): en trupp-vid lista med anmälningsläge, nådd från menyn. Varje cup
 * länkar till sin händelsesida där man anmäler sitt barn (`#296`).
 */

function token(): string {
  return `x.${btoa(JSON.stringify({ email: 'm@example.com', sub: 'me' }))}.y`
}

function stub(cups: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: unknown) => {
      const url = String(input)
      if (url.includes('/auth/csrf')) return Promise.resolve(jsonResponse({ token: 'csrf' }))
      if (url.includes('/auth/refresh'))
        return Promise.resolve(jsonResponse({ accessToken: token() }))
      if (url.endsWith('/api/v1/trupper/mina')) {
        return Promise.resolve(
          jsonResponse([
            { id: 'trupp-1', clubName: 'Kärra', name: 'P2016', season: '2026', isLeader: false },
          ]),
        )
      }
      if (url.endsWith('/trupper/trupp-1/cups')) return Promise.resolve(jsonResponse(cups))
      return Promise.resolve(jsonResponse({}))
    }),
  )
}

beforeEach(() => {
  localStorage.clear()
  clearSession()
  setAccessToken(token())
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('cup-sidan', () => {
  it('listar truppens cuper med platser kvar och länkar till händelsen', async () => {
    stub([
      {
        eventId: 'e1',
        title: 'Sommarcup',
        kickoffUtc: '2026-09-20T08:00:00Z',
        teamName: 'Svart',
        colorHex: '#161616',
        open: true,
        capacity: 10,
        spotsLeft: 4,
        isFull: false,
      },
    ])

    renderRoute('/cuper/trupp-1')

    expect(await screen.findByText('Sommarcup')).toBeInTheDocument()
    expect(screen.getByText('4 av 10 platser kvar')).toBeInTheDocument()

    const link = screen.getByRole('link', { name: /Sommarcup/ })
    expect(link).toHaveAttribute('href', '/handelse/e1')
  })

  it('säger till när det inte finns några cuper', async () => {
    stub([])

    renderRoute('/cuper/trupp-1')

    expect(await screen.findByText('Inga cuper än.')).toBeInTheDocument()
  })
})
