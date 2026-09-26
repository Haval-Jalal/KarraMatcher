import { useEffect, useState } from 'react'

import { isInstalled, isIos } from '@/lib/platform'
import { readSetting, writeSetting } from '@/lib/storage'

/**
 * En diskret uppmaning att lägga appen på hemskärmen (§KM.8).
 *
 * <h3>Varför app-brett och inte bara i inställningarna</h3>
 *
 * <para>
 * På iOS krävs hemskärms-installation för att notiser ska fungera, och Safari rensar lagringen
 * för sajter som inte besökts på en vecka — dödligt för en app som används säsongsvis. Tipset
 * fanns bara begravt i notis-inställningarna; här möter det föräldern där hen är, en gång.
 * </para>
 *
 * <h3>Diskret, och tjatar aldrig</h3>
 *
 * <para>
 * Visas bara när den kan hjälpa — appen är inte redan installerad, och antingen är det iOS (där
 * installationen är en handpåläggning: Dela → Lägg till på hemskärmen) eller så har webbläsaren
 * erbjudit en riktig installations-prompt (Android/dator). Stänger man den kommer den inte
 * tillbaka: valet sparas på enheten.
 * </para>
 */

const DISMISSED_KEY = 'install-banner-dismissed'

/** Den lilla del av webbläsarens installations-event vi använder (saknas i lib.dom). */
interface InstallPromptEvent extends Event {
  prompt: () => Promise<void>
}

export function InstallBanner() {
  const [dismissed, setDismissed] = useState(() => readSetting(DISMISSED_KEY) === '1')
  const [prompt, setPrompt] = useState<InstallPromptEvent | null>(null)

  useEffect(() => {
    function onBeforeInstallPrompt(event: Event): void {
      // Hindra webbläsarens egen mini-infobar och spara eventet, så vi kan visa knappen där
      // den passar in i appen i stället.
      event.preventDefault()
      setPrompt(event as InstallPromptEvent)
    }

    window.addEventListener('beforeinstallprompt', onBeforeInstallPrompt)

    return () => {
      window.removeEventListener('beforeinstallprompt', onBeforeInstallPrompt)
    }
  }, [])

  if (dismissed || isInstalled()) {
    return null
  }

  const ios = isIos()

  // Bara när vi faktiskt kan hjälpa: en iOS-instruktion, eller en fångad installations-prompt.
  if (!ios && prompt === null) {
    return null
  }

  function dismiss(): void {
    writeSetting(DISMISSED_KEY, '1')
    setDismissed(true)
  }

  async function install(): Promise<void> {
    if (prompt === null) {
      return
    }

    await prompt.prompt()
    // Oavsett svar: göm bannern. Avböjer man kan man installera via webbläsarens meny senare.
    dismiss()
  }

  return (
    <div className="install-banner" role="note">
      <p className="install-banner__text">
        {ios ? (
          <>
            Lägg appen på hemskärmen så fungerar notiser och schemat laddas snabbare: tryck{' '}
            <b>Dela</b> och välj <b>Lägg till på hemskärmen</b>.
          </>
        ) : (
          <>Installera appen för snabbare start och notiser.</>
        )}
      </p>

      <div className="install-banner__actions">
        {!ios && prompt !== null && (
          <button
            type="button"
            className="button button--small"
            onClick={() => {
              void install()
            }}
          >
            Installera
          </button>
        )}

        <button
          type="button"
          className="install-banner__dismiss"
          aria-label="Stäng"
          onClick={dismiss}
        >
          <span aria-hidden="true">✕</span>
        </button>
      </div>
    </div>
  )
}
