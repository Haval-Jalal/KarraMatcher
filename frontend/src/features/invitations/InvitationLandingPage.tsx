import { Link, useParams } from '@tanstack/react-router'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { ApiError } from '@/lib/api'

import { useAcceptInvitation, useInvitationPreview } from './useInvitations'

/**
 * Förälderns landningssida för en inbjudan (§KM.3, `#193`).
 *
 * Anonymt läsbar: en inbjuden förälder ska kunna se vart länken leder innan hen loggar in.
 * Accepten kräver inloggning som just den adress inbjudan gäller — servern nekar annars, och
 * sidan säger det innan man ens försöker.
 */
export function InvitationLandingPage() {
  const { token } = useParams({ from: '/inbjudan/$token' })
  const { status, email } = useAuth()
  const preview = useInvitationPreview(token)
  const accept = useAcceptInvitation(token)
  const [accepted, setAccepted] = useState<string | null>(null)
  const [failure, setFailure] = useState<string | null>(null)

  if (preview.isLoading || status === 'okand') {
    return (
      <main className="page">
        <p className="state">Hämtar inbjudan…</p>
      </main>
    )
  }

  if (preview.isError || preview.data === undefined) {
    return (
      <main className="page">
        <h1>Inbjudan finns inte</h1>
        <p className="state">Länken är fel eller gäller inte längre.</p>
        <Link to="/">Till startsidan</Link>
      </main>
    )
  }

  const { valid, truppName, email: invitedEmail } = preview.data

  if (accepted !== null) {
    return (
      <main className="page">
        <h1>Du är med!</h1>
        <p className="state state--ok" role="status">
          Du är nu med i {accepted}.
        </p>
        <Link to="/">Till matcherna</Link>
      </main>
    )
  }

  return (
    <main className="page">
      <header className="app-header">
        <h1>Inbjudan till {truppName}</h1>
        <p>Du har blivit inbjuden som vårdnadshavare.</p>
      </header>

      {!valid && (
        <p className="state state--error" role="alert">
          Den här inbjudan gäller inte längre. Be admin skicka en ny.
        </p>
      )}

      {valid && status === 'utloggad' && (
        <div className="admin-section">
          <p>
            Logga in med <strong>{invitedEmail}</strong> för att gå med.
          </p>
          <Link className="button" to="/logga-in" search={{ next: `/inbjudan/${token}` }}>
            Logga in
          </Link>
        </div>
      )}

      {valid && status === 'inloggad' && email !== invitedEmail && (
        <p className="state state--error" role="alert">
          Du är inloggad som {email}, men inbjudan gäller {invitedEmail}. Logga ut och logga in med
          rätt adress.
        </p>
      )}

      {valid && status === 'inloggad' && email === invitedEmail && (
        <div className="admin-section">
          <p>
            Gå med i <strong>{truppName}</strong>.
          </p>
          {failure !== null && (
            <p className="state state--error" role="alert">
              {failure}
            </p>
          )}
          <button
            type="button"
            className="button"
            disabled={accept.isPending}
            onClick={() => {
              setFailure(null)
              void accept
                .mutateAsync()
                .then((result) => setAccepted(result.truppName || truppName))
                .catch((error: unknown) => {
                  setFailure(
                    error instanceof ApiError
                      ? error.message
                      : 'Det gick inte att gå med just nu. Försök igen.',
                  )
                })
            }}
          >
            {accept.isPending ? 'Går med…' : 'Gå med'}
          </button>
        </div>
      )}
    </main>
  )
}
