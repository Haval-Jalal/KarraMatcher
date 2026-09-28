import { useNavigate, useParams } from '@tanstack/react-router'

import { useAuth } from '@/features/auth'
import { useMyTrupper } from '@/features/chat'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { BarnOchLag } from './BarnOchLag'

/**
 * "Truppen" — adminens egen sida för hela truppen (`#redesign`): barn, färg-lag och
 * vårdnadshavare. En riktig route (`/trupp/$truppId`) i stället för en flik inne i `/admin`, så
 * bakåtknapp, delbara länkar och skärmläsare fungerar som för vilken sida som helst.
 *
 * <h3>Samma vy, en sanningskälla</h3>
 *
 * Innehållet är {@link BarnOchLag} — samma komponent som `/admin`-fliken "Barn & lag" använder.
 * Ingen dubblerad roster-UI; adminvyn kan peka hit.
 *
 * <h3>Synlighet vs säkerhet</h3>
 *
 * Vyn göms för den som inte är admin för just den här truppen, men det är bara bekvämlighet —
 * servern (policyn <c>AdminOfTrupp</c>) är den riktiga grinden (§KM.3). Route-grinden kräver
 * bara inloggning.
 */
export function TruppPage() {
  const { truppId } = useParams({ from: '/trupp/$truppId' })
  const { isSuperAdmin, adminOf } = useAuth()
  const trupper = useMyTrupper()
  const navigate = useNavigate()

  useDocumentTitle('Truppen')

  if (!isSuperAdmin && !adminOf.includes(truppId)) {
    return (
      <main className="page">
        <header className="app-header">
          <h1>Ingen behörighet</h1>
        </header>
        <p className="state">Den här vyn är för truppens tränare.</p>
      </main>
    )
  }

  // Väljaren visas bara för den som är admin i flera trupper; annars är den här truppen given.
  const options = (trupper.data ?? []).filter((trupp) => isSuperAdmin || adminOf.includes(trupp.id))

  return (
    <main className="page">
      <header className="app-header">
        <h1>Truppen</h1>
        <p className="app-header__subtitle">Barn, färg-lag och vårdnadshavare.</p>
      </header>

      {options.length > 1 && (
        <div className="admin-trupp-picker form__field">
          <label htmlFor="trupp-valj">Trupp</label>
          <select
            id="trupp-valj"
            value={truppId}
            onChange={(event) => {
              void navigate({ to: '/trupp/$truppId', params: { truppId: event.target.value } })
            }}
          >
            {options.map((trupp) => (
              <option key={trupp.id} value={trupp.id}>
                {trupp.clubName} · {trupp.name} {trupp.season}
              </option>
            ))}
          </select>
        </div>
      )}

      <BarnOchLag truppId={truppId} />
    </main>
  )
}
