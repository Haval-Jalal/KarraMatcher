import { LoadingState } from '@/components/LoadingState'
import { NotificationSettings } from '@/features/notifications'
import { useTeams } from '@/features/teams'
import { ApiError } from '@/lib/api'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

/**
 * Inställningar — appens samlade inställningar, nådd via "Mer".
 *
 * <h3>Notiser hör inte hemma i schemat</h3>
 *
 * <para>
 * Notisinställningarna låg förr inbäddade i lag-schemat. En inställning är ingen del av
 * helgens matcher — den hör hemma här, samlat. Notiser är per lag (§KM.7/`#65`), så vyn
 * listar de lag du är med i, var och en med sina val. Framåt bor fler app-inställningar här.
 * </para>
 *
 * <h3>Bara för en inloggad</h3>
 *
 * <para>
 * Inställningarna hör till ett konto (§KM.3); routen kräver inloggning, och varje lags ruta
 * visar bara den inloggades egna val.
 * </para>
 */
export function SettingsPage() {
  useDocumentTitle('Inställningar')

  const teams = useTeams()

  return (
    <main>
      <header className="app-header">
        <h1>Inställningar</h1>
        <p className="app-header__subtitle">Välj vilka notiser du vill ha, per lag.</p>
      </header>

      {teams.isPending && <LoadingState label="Hämtar lagen…" />}

      {teams.isError && (
        <p className="state state--error" role="alert">
          {teams.error instanceof ApiError && teams.error.offline
            ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
            : 'Kunde inte hämta lagen just nu.'}
        </p>
      )}

      {teams.data && teams.data.length === 0 && (
        <p className="state">Du är inte med i något lag än.</p>
      )}

      {teams.data?.map((team) => (
        <NotificationSettings
          key={team.slug}
          teamSlug={team.slug}
          title={`${team.ageGroup} ${team.name}`}
        />
      ))}
    </main>
  )
}
