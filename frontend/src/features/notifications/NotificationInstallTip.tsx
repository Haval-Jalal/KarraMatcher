import { isInstalled, isIos } from '@/lib/platform'

/**
 * Tipset om att notiser på iOS kräver hemskärm-installation (`#65`, §KM.8).
 *
 * <para>
 * På iPhone och iPad får en webbplats inte skicka notiser förrän den ligger på hemskärmen —
 * till skillnad från Android och dator. Utan det här blir växlarna ovan en inställning för
 * något som ändå inte kan hända, och föräldern undrar varför inget kommer. Visas bara på
 * iOS, och bara när appen inte redan är installerad.
 * </para>
 */
export function NotificationInstallTip() {
  if (!isIos() || isInstalled()) {
    return null
  }

  return (
    <p className="notif-settings__tip" role="note">
      På iPhone och iPad krävs att appen ligger på hemskärmen för att notiser ska fungera. Tryck på{' '}
      <b>Dela</b> längst ner och välj <b>Lägg till på hemskärmen</b>.
    </p>
  )
}
