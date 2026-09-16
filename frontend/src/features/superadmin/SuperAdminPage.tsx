import { useAuth } from '@/features/auth'

import { SlugEntitySection } from './SlugEntitySection'
import { TruppSection } from './TruppSection'
import { TruppWorkspace } from './TruppWorkspace'
import {
  useClubs,
  useCreateClub,
  useCreateSport,
  useSports,
  useTrupper,
  useUpdateClub,
  useUpdateSport,
} from './useSuperadmin'

/**
 * Superadmin-konsolen (§KM.3, `#192`): skapa och ändra sport, klubb, trupp och lag, och
 * tillsätt admins per trupp.
 *
 * <h3>Synligheten är inte säkerheten</h3>
 *
 * Vyn göms för alla utom superadmin, men det är servern (policyn <c>SuperAdmin</c>) som
 * nekar — varje anrop härifrån svarar `403` för alla andra. Grinden i routern kontrollerar
 * bara att man är inloggad; rollkontrollen här styr bara vad som visas.
 */
export function SuperAdminPage() {
  const { status, isSuperAdmin } = useAuth()

  const sports = useSports()
  const clubs = useClubs()
  const trupper = useTrupper()
  const createSport = useCreateSport()
  const updateSport = useUpdateSport()
  const createClub = useCreateClub()
  const updateClub = useUpdateClub()

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
        <p>Sporter, klubbar, trupper, lag och admins.</p>
      </header>

      <SlugEntitySection
        title="Sporter"
        singular="sport"
        items={sports.data ?? []}
        isLoading={sports.isLoading}
        isError={sports.isError}
        onCreate={(name, slug) => createSport.mutateAsync({ name, slug })}
        onRename={(id, name) => updateSport.mutateAsync({ id, name })}
      />

      <SlugEntitySection
        title="Klubbar"
        singular="klubb"
        items={clubs.data ?? []}
        isLoading={clubs.isLoading}
        isError={clubs.isError}
        onCreate={(name, slug) => createClub.mutateAsync({ name, slug })}
        onRename={(id, name) => updateClub.mutateAsync({ id, name })}
      />

      <TruppSection clubs={clubs.data ?? []} sports={sports.data ?? []} />

      <TruppWorkspace trupper={trupper.data ?? []} />
    </main>
  )
}
