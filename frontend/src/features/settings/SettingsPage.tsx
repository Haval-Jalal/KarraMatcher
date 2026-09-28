import { DevicePushToggle } from '@/features/notifications'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

import { ThemeToggle } from './ThemeToggle'

/**
 * Inställningar — appens samlade inställningar, nådd via "Mer".
 *
 * <h3>Notiser hör inte hemma i schemat</h3>
 *
 * <para>
 * Notisinställningarna låg förr inbäddade i lag-schemat. En inställning är ingen del av
 * helgens matcher — den hör hemma här. Notiser är numera <b>en enda växel per enhet</b>
 * (`#332`-uppföljning): på = notiser hit, av = inga — men kritiska besked når ändå fram via
 * mejl. Ingen separat global flagga, inga per-lag-val.
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

      <section className="notif-settings" aria-labelledby="notiser">
        <h2 id="notiser">Notiser</h2>
        <p className="notif-settings__lead">
          Slå på för att få notiser på den här telefonen — händelser, kallelser, samåkning och
          chatt. Varje enhet slås på för sig. Stänger du av når du ändå kritiska besked (kallelse,
          inställd eller flyttad match) via mejl, så du inte missar något viktigt.
        </p>

        <DevicePushToggle />
      </section>

      <section className="notif-settings" aria-labelledby="utseende">
        <h2 id="utseende">Utseende</h2>
        <p className="notif-settings__lead">
          Välj hur appen ser ut på den här enheten. <strong>System</strong> följer telefonens läge;
          välj <strong>Ljust</strong> eller <strong>Mörkt</strong> för att bestämma själv.
        </p>

        <ThemeToggle />
      </section>
    </main>
  )
}
