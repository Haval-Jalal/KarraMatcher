import { DevicePushToggle, NotificationSettings } from '@/features/notifications'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

/**
 * Inställningar — appens samlade inställningar, nådd via "Mer".
 *
 * <h3>Notiser hör inte hemma i schemat</h3>
 *
 * <para>
 * Notisinställningarna låg förr inbäddade i lag-schemat. En inställning är ingen del av
 * helgens matcher — den hör hemma här. Notiser är numera en <b>enda global på/av</b> per
 * konto (`#332`-uppföljning, ersätter per-typ/per-lag `#65`); därunder styr man push per
 * enhet och lag.
 * </para>
 *
 * <h3>Bara för en inloggad</h3>
 *
 * <para>
 * Inställningarna hör till ett konto (§KM.3); routen kräver inloggning.
 * </para>
 */
export function SettingsPage() {
  useDocumentTitle('Inställningar')

  return (
    <main>
      <header className="app-header">
        <h1>Inställningar</h1>
        <p className="app-header__subtitle">Slå på eller av notiser, och välj enhet.</p>
      </header>

      <NotificationSettings />

      <section className="notif-settings" aria-labelledby="enheter">
        <h2 id="enheter">På den här enheten</h2>
        <p className="notif-settings__lead">
          Push når bara enheter du slagit på. Det gäller den här telefonen — varje enhet slås på för
          sig.
        </p>

        <DevicePushToggle />
      </section>
    </main>
  )
}
