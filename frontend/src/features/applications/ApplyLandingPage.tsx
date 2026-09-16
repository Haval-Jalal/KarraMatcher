import { Link, useParams } from '@tanstack/react-router'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { ApiError } from '@/lib/api'

import { useApply, useApplyInfo } from './useApplications'

/**
 * Förälderns ansökningssida (§KM.3, `#194`).
 *
 * Nås via en länk klubben delar (`/ansok/{truppId}`). Trupp-infon är anonymt läsbar; själva
 * ansökan kräver inloggning. En admin godkänner sedan i kön (`#194`).
 */
export function ApplyLandingPage() {
  const { truppId } = useParams({ from: '/ansok/$truppId' })
  const { status } = useAuth()
  // Trupp-infon kräver inloggning (§KM.3) — hämtas därför först när man är inloggad.
  const info = useApplyInfo(truppId, status === 'inloggad')
  const apply = useApply(truppId)
  const [submitted, setSubmitted] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)

  if (status === 'okand') {
    return (
      <main className="page">
        <p className="state">Hämtar…</p>
      </main>
    )
  }

  if (status === 'utloggad') {
    return (
      <main className="page">
        <header className="app-header">
          <h1>Gå med i en trupp</h1>
          <p>Ansök om medlemskap som vårdnadshavare.</p>
        </header>
        <div className="admin-section">
          <p>Logga in för att ansöka.</p>
          <Link className="button" to="/logga-in" search={{ next: `/ansok/${truppId}` }}>
            Logga in
          </Link>
        </div>
      </main>
    )
  }

  if (info.isLoading) {
    return (
      <main className="page">
        <p className="state">Hämtar…</p>
      </main>
    )
  }

  if (info.isError || info.data === undefined) {
    return (
      <main className="page">
        <h1>Truppen finns inte</h1>
        <p className="state">Kontrollera länken.</p>
        <Link to="/">Till startsidan</Link>
      </main>
    )
  }

  const { truppName } = info.data

  if (submitted) {
    return (
      <main className="page">
        <h1>Ansökan inskickad</h1>
        <p className="state state--ok" role="status">
          Din ansökan till {truppName} är inskickad. En admin godkänner den.
        </p>
        <Link to="/">Till startsidan</Link>
      </main>
    )
  }

  return (
    <main className="page">
      <header className="app-header">
        <h1>Gå med i {truppName}</h1>
        <p>Ansök om att bli medlem som vårdnadshavare.</p>
      </header>

      <div className="admin-section">
        {failure !== null && (
          <p className="state state--error" role="alert">
            {failure}
          </p>
        )}
        <button
          type="button"
          className="button"
          disabled={apply.isPending}
          onClick={() => {
            setFailure(null)
            void apply
              .mutateAsync()
              .then(() => setSubmitted(true))
              .catch((error: unknown) => {
                setFailure(
                  error instanceof ApiError
                    ? error.message
                    : 'Det gick inte att ansöka just nu. Försök igen.',
                )
              })
          }}
        >
          {apply.isPending ? 'Skickar…' : 'Ansök om att gå med'}
        </button>
      </div>
    </main>
  )
}
