import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import { useAuth } from '@/features/auth'

import { saveNotificationSettings } from './notificationsApi'
import { notificationSettingsQueryKey, useNotificationSettings } from './useNotificationSettings'

/**
 * Kontots globala notisinställning — en enda på/av (`#332`-uppföljning, ersätter per-typ `#65`).
 *
 * <h3>Bara för en inloggad</h3>
 *
 * En inställning hör till ett konto (§KM.3). En gäst ser ingenting här.
 *
 * <h3>Av gäller direkt</h3>
 *
 * Växlingen sparas med en gång, och servern filtrerar utskicken efter det — avstängt är
 * avstängt vid nästa notis. Kritiska besked (kallelse, inställd/flyttad match) når ändå fram
 * via mejl, så man missar inget. Ändringen visas optimistiskt och rullas tillbaka om
 * sparningen misslyckas.
 */
export function NotificationSettings() {
  const { status } = useAuth()
  const queryClient = useQueryClient()
  const [saving, setSaving] = useState(false)
  const [failed, setFailed] = useState(false)

  const isSignedIn = status === 'inloggad'
  const { data } = useNotificationSettings(isSignedIn)

  // Gäst eller ännu inte hämtad: ingenting. Inställningen hör till ett konto.
  if (!isSignedIn || data === undefined) {
    return null
  }

  const settings = data

  async function toggle(): Promise<void> {
    const next = { enabled: !settings.enabled }

    setSaving(true)
    setFailed(false)
    queryClient.setQueryData(notificationSettingsQueryKey, next)

    try {
      const saved = await saveNotificationSettings(next)
      queryClient.setQueryData(notificationSettingsQueryKey, saved)
    } catch {
      // Tillbaka till det som gällde -- annars ser en misslyckad sparning ut som en lyckad.
      queryClient.setQueryData(notificationSettingsQueryKey, settings)
      setFailed(true)
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="notif-settings" aria-labelledby="notiser">
      <h2 id="notiser">Notiser</h2>

      <label className="notif-settings__row">
        <input
          type="checkbox"
          checked={settings.enabled}
          disabled={saving}
          onChange={() => {
            void toggle()
          }}
        />
        <span>
          <strong>Push-notiser</strong>
          <br />
          Händelser, kallelser, samåkning och chatt. Stänger du av når du ändå kritiska besked
          (kallelse, inställd eller flyttad match) via mejl.
        </span>
      </label>

      {failed && (
        <p className="state state--error" role="alert">
          Det gick inte att spara just nu. Försök igen om en stund.
        </p>
      )}
    </section>
  )
}
