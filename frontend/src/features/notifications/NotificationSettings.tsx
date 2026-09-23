import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'

import { useAuth } from '@/features/auth'

import { DevicePushToggle } from './DevicePushToggle'
import { saveNotificationSettings, type NotificationSettings as Settings } from './notificationsApi'
import { notificationSettingsQueryKey, useNotificationSettings } from './useNotificationSettings'

/**
 * En förälders notisinställningar för ett lag (`#65`).
 *
 * <h3>Bara för en inloggad</h3>
 *
 * En inställning hör till ett konto (§KM.3). En gäst ser ingenting här — det finns inget att
 * spara på, och en tom ruta vore bara förvirrande.
 *
 * <h3>Avstängning gäller direkt</h3>
 *
 * Varje växling sparas med en gång, och servern filtrerar utskicken efter det — avstängt är
 * avstängt vid nästa notis, inte bara dolt i gränssnittet. Ändringen visas optimistiskt och
 * rullas tillbaka om sparningen misslyckas.
 */

const ROWS: { key: keyof Settings; label: string; description: string }[] = [
  {
    key: 'eventChanges',
    label: 'Händelser',
    description: 'Ny, flyttad, ändrad eller inställd händelse, och påminnelsen kvällen före.',
  },
  { key: 'kallelser', label: 'Kallelser', description: 'Ny kallelse och påminnelse att svara.' },
  { key: 'carpool', label: 'Samåkning', description: 'Erbjudanden, förfrågningar och svar.' },
  { key: 'chat', label: 'Chatt', description: 'Meddelanden i lagets och truppens chatt.' },
]

export function NotificationSettings({ teamSlug }: { teamSlug: string }) {
  const { status } = useAuth()
  const queryClient = useQueryClient()
  const [saving, setSaving] = useState(false)
  const [failed, setFailed] = useState(false)

  const isSignedIn = status === 'inloggad'
  const { data } = useNotificationSettings(teamSlug, isSignedIn)

  // Gäst eller ännu inte hämtad: ingenting. Inställningen hör till ett konto.
  if (!isSignedIn || data === undefined) {
    return null
  }

  const settings = data

  async function toggle(key: keyof Settings): Promise<void> {
    const next: Settings = { ...settings }
    next[key] = !next[key]

    setSaving(true)
    setFailed(false)
    queryClient.setQueryData(notificationSettingsQueryKey(teamSlug), next)

    try {
      const saved = await saveNotificationSettings(teamSlug, next)
      queryClient.setQueryData(notificationSettingsQueryKey(teamSlug), saved)
    } catch {
      // Tillbaka till det som gällde -- annars ser en misslyckad sparning ut som en lyckad.
      queryClient.setQueryData(notificationSettingsQueryKey(teamSlug), settings)
      setFailed(true)
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="notif-settings" aria-labelledby="notiser">
      <h2 id="notiser">Notiser</h2>

      <DevicePushToggle teamSlug={teamSlug} />

      <p className="notif-settings__lead">Välj vilka notiser du vill ha för det här laget.</p>

      {ROWS.map(({ key, label, description }) => (
        <label key={key} className="notif-settings__row">
          <input
            type="checkbox"
            checked={settings[key]}
            disabled={saving}
            onChange={() => {
              void toggle(key)
            }}
          />
          <span>
            <strong>{label}</strong>
            <br />
            {description}
          </span>
        </label>
      ))}

      {failed && (
        <p className="state state--error" role="alert">
          Det gick inte att spara just nu. Försök igen om en stund.
        </p>
      )}
    </section>
  )
}
