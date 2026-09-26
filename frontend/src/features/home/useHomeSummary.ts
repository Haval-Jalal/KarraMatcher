import { useQuery } from '@tanstack/react-query'

import { getHomeSummary } from './homeApi'

export const homeSummaryQueryKey = ['home-summary'] as const

/**
 * Hem-vyns sammanställning. Stängt innehåll som aldrig cachas på edgen (§KM.8) — hämtas färskt
 * varje gång, till skillnad från den publika lag-listan.
 */
export function useHomeSummary() {
  return useQuery({
    queryKey: homeSummaryQueryKey,
    queryFn: ({ signal }) => getHomeSummary(signal),
  })
}
