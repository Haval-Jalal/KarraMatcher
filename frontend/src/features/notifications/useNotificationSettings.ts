import { useQuery } from '@tanstack/react-query'

import { getNotificationSettings } from './notificationsApi'

export const notificationSettingsQueryKey = (teamSlug: string) =>
  ['notification-settings', teamSlug] as const

/**
 * En förälders notisinställningar för ett lag.
 *
 * Bara för en inloggad — en gäst har inget konto att spara på. Kort färskhet spelar ingen
 * roll här: inställningen ändras sällan och bara av användaren själv.
 */
export function useNotificationSettings(teamSlug: string, enabled: boolean) {
  return useQuery({
    queryKey: notificationSettingsQueryKey(teamSlug),
    queryFn: ({ signal }) => getNotificationSettings(teamSlug, signal),
    enabled,
  })
}
