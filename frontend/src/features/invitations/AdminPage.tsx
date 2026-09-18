import { useState } from 'react'

import { ApplicationsPanel } from '@/features/applications'
import { useAuth } from '@/features/auth'
import { ChildrenPanel } from '@/features/children'
import { CoachesPanel } from '@/features/coaches'

import { InvitationsPanel } from './InvitationsPanel'
import { useMyTrupper } from './useInvitations'

/**
 * Trupp-adminens vy (§KM.3, `#193`): bjud in vårdnadshavare till sina trupper.
 *
 * <h3>Synligheten är inte säkerheten</h3>
 *
 * Vyn göms för den som inte är admin för någon trupp, men servern (policyn
 * <c>AdminOfTrupp</c>) är den riktiga grinden. Route-grinden kräver bara inloggning.
 */
export function AdminPage() {
  const { status, isSuperAdmin, adminOf } = useAuth()
  const trupper = useMyTrupper()
  const [truppId, setTruppId] = useState<string | null>(null)

  if (status === 'okand') {
    return (
      <main className="page">
        <p className="state">Laddar…</p>
      </main>
    )
  }

  if (!isSuperAdmin && adminOf.length === 0) {
    return (
      <main className="page">
        <h1>Ingen behörighet</h1>
        <p className="state">Den här vyn är för trupp-admins.</p>
      </main>
    )
  }

  const options = trupper.data ?? []

  return (
    <main className="page superadmin">
      <header className="app-header">
        <h1>Admin</h1>
        <p>Bjud in vårdnadshavare till dina trupper.</p>
      </header>

      <section className="admin-section" aria-labelledby="admin-trupp-rubrik">
        <h2 id="admin-trupp-rubrik">Trupp</h2>

        {trupper.isLoading && <p className="state">Hämtar dina trupper…</p>}
        {trupper.isError && (
          <p className="state state--error" role="alert">
            Kunde inte hämta dina trupper.
          </p>
        )}

        {trupper.data && options.length === 0 && (
          <p className="state">Du är inte admin för någon trupp än.</p>
        )}

        {options.length > 0 && (
          <div className="form__field">
            <label htmlFor="admin-valj-trupp">Välj trupp</label>
            <select
              id="admin-valj-trupp"
              value={truppId ?? ''}
              onChange={(event) =>
                setTruppId(event.target.value === '' ? null : event.target.value)
              }
            >
              <option value="">Välj trupp…</option>
              {options.map((trupp) => (
                <option key={trupp.id} value={trupp.id}>
                  {trupp.clubName} · {trupp.name} {trupp.season}
                </option>
              ))}
            </select>
          </div>
        )}

        {truppId !== null && (
          <>
            <InvitationsPanel truppId={truppId} />
            <ApplicationsPanel truppId={truppId} />
            <CoachesPanel truppId={truppId} />
            <ChildrenPanel truppId={truppId} />
          </>
        )}
      </section>
    </main>
  )
}
