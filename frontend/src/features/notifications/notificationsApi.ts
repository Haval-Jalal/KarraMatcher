import { getAuthJson, postJson } from '@/lib/api'

/**
 * Kontots globala notisinställning — en enda på/av (`#332`-uppföljning, ersätter per-typ `#65`).
 *
 * <h3>En växel per konto</h3>
 *
 * Av = ingen push; kritiska besked (kallelse, inställd/flyttad match) når ändå fram via mejl.
 * På är förvalet — servern svarar med `enabled: true` för den som aldrig ändrat något.
 */
export interface NotificationSettings {
  /** Om kontot vill ha push-notiser alls. */
  enabled: boolean
}

const url = '/api/v1/notification-settings'

/** Min globala notisinställning. Kräver konto. */
export function getNotificationSettings(signal?: AbortSignal): Promise<NotificationSettings> {
  return getAuthJson<NotificationSettings>(url, signal)
}

/** Slår på eller av mina notiser. Gäller vid nästa utskick. */
export function saveNotificationSettings(
  settings: NotificationSettings,
): Promise<NotificationSettings> {
  return postJson<NotificationSettings>(url, settings, { method: 'PUT' })
}
