import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { getCalendarLink, regenerateCalendarLink, type CalendarLink } from './calendarApi'

export const calendarLinkQueryKey = ['calendar-link'] as const

/** Kontots kalender-länk. Skapas server-side vid första hämtningen. */
export function useCalendarLink() {
  return useQuery({
    queryKey: calendarLinkQueryKey,
    queryFn: ({ signal }) => getCalendarLink(signal),
  })
}

/** Byter ut kalender-nyckeln och lägger den nya länken i cachen. */
export function useRegenerateCalendarLink() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: regenerateCalendarLink,
    onSuccess: (link: CalendarLink) => {
      queryClient.setQueryData(calendarLinkQueryKey, link)
    },
  })
}
