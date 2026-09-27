import { useQuery } from '@tanstack/react-query'

import { getNotificationSettings } from './notificationsApi'

export const notificationSettingsQueryKey = ['notification-settings'] as const

/**
 * Kontots globala notisinställning.
 *
 * Bara för en inloggad — en gäst har inget konto att spara på. Kort färskhet spelar ingen
 * roll här: inställningen ändras sällan och bara av användaren själv.
 */
export function useNotificationSettings(enabled: boolean) {
  return useQuery({
    queryKey: notificationSettingsQueryKey,
    queryFn: ({ signal }) => getNotificationSettings(signal),
    enabled,
  })
}
