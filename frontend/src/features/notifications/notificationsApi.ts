import { getAuthJson, postJson } from '@/lib/api'

/**
 * En förälders notisinställningar för ett lag (`#65`).
 *
 * <h3>Per lag, en växel per notistyp</h3>
 *
 * En förälder med barn i två lag ska kunna ha olika inställningar för dem. Allt på är
 * förvalet — servern svarar med alla <c>true</c> för den som aldrig ändrat något. Chatt
 * finns med redan nu (`#200`) fast utskicket byggs i `#201`/`#202`.
 */
export interface NotificationSettings {
  /** Händelse skapad, flyttad, ändrad eller inställd, samt kvällspåminnelsen. */
  eventChanges: boolean
  /** Kallelser: ny kallelse och påminnelse att svara. */
  kallelser: boolean
  /** Samåkning: erbjudande, förfrågan, svar. */
  carpool: boolean
  /** Chatt (byggs senare). */
  chat: boolean
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
