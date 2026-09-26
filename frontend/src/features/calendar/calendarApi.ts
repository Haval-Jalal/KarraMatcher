import { getAuthJson, postJson } from '@/lib/api'

/** Kontots personliga kalender-länk att prenumerera på. Speglar `CalendarLinkDto` i backend. */
export interface CalendarLink {
  url: string
}

/** Hämtar (och skapar vid behov) kontots kalender-länk. */
export const getCalendarLink = (signal?: AbortSignal): Promise<CalendarLink> =>
  getAuthJson<CalendarLink>('/api/v1/kalender/min', signal)

/** Byter ut nyckeln — den gamla länken slutar fungera direkt. */
export const regenerateCalendarLink = (): Promise<CalendarLink> =>
  postJson<CalendarLink>('/api/v1/kalender/aterkalla')
