import { useAuth } from '@/features/auth'

import { SetupWizard } from './SetupWizard'

/**
 * Superadmin-konsolen (§KM.3, `#192`, `#261`).
 *
 * <h3>En guidad kedja, inte fyra högar</h3>
 *
 * Superadmins jobb är en sekvens: skapa (eller välj) en sport, en klubb, en trupp, och
 * tilldela en admin. Guiden speglar den kedjan. Resten — lag, barn, tränare, inbjudningar —
 * är truppens admins ansvar och sköts i admin-vyn (`/admin`).
 *
 * <h3>Synligheten är inte säkerheten</h3>
 *
 * Vyn göms för alla utom superadmin, men det är servern (policyn <c>SuperAdmin</c>) som
 * nekar — varje anrop härifrån svarar `403` för alla andra.
 */
export function SuperAdminPage() {
  const { status, isSuperAdmin } = useAuth()

  if (status === 'okand') {
    return (
      <main className="page">
        <p className="state">Laddar…</p>
      </main>
    )
  }

  if (!isSuperAdmin) {
    return (
      <main className="page">
        <h1>Ingen behörighet</h1>
        <p className="state">Den här vyn är bara för superadmin.</p>
      </main>
    )
  }

  return (
    <main className="page superadmin">
      <header className="app-header">
        <h1>Superadmin</h1>
        <p>Skapa en sport, en klubb och en trupp — och tilldela en tränare som sköter resten.</p>
      </header>

      <SetupWizard />
    </main>
  )
}
