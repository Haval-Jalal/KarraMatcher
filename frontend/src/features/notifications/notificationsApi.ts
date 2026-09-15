import { getAuthJson, postJson } from '@/lib/api'

/**
 * En förälders notisinställningar för ett lag (`#65`).
 *
 * <h3>Per lag, tre växlar</h3>
 *
 * En förälder med barn i två lag ska kunna ha olika inställningar för dem. Allt på är
 * förvalet — servern svarar med tre <c>true</c> för den som aldrig ändrat något.
 */
export interface NotificationSettings {
  /** Ny, flyttad, ändrad eller inställd match. */
  matchChanges: boolean
  /** Samåkning: erbjudande, förfrågan, svar. */
  carpool: boolean
  /** Påminnelser: kvällen före match och kallelsen. */
  reminders: boolean
}

const base = (teamSlug: string) =>
  `/api/v1/teams/${encodeURIComponent(teamSlug)}/notification-settings`

/** Mina inställningar för laget. Kräver konto. */
export function getNotificationSettings(
  teamSlug: string,
  signal?: AbortSignal,
): Promise<NotificationSettings> {
  return getAuthJson<NotificationSettings>(base(teamSlug), signal)
}

/** Sätter mina inställningar för laget. Gäller vid nästa utskick. */
export function saveNotificationSettings(
  teamSlug: string,
  settings: NotificationSettings,
): Promise<NotificationSettings> {
  return postJson<NotificationSettings>(base(teamSlug), settings, { method: 'PUT' })
}
