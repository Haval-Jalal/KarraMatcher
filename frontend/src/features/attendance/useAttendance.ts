import { useQuery } from '@tanstack/react-query'

import { ApiError } from '@/lib/api'

import { getAttendanceState } from './attendanceApi'

export const attendanceStateQueryKey = (matchId: string) => ['attendance', matchId] as const

/**
 * Mitt eget närvaroläge på en match.
 *
 * <h3>404 är inte ett fel här</h3>
 *
 * Är kallelsen avslagen för laget svarar servern `404` (§KM.7). Det är ett giltigt svar —
 * funktionen finns inte för det här laget — så hämtningen försöker inte igen på just det.
 * Anroparen skiljer på `404` och ett riktigt fel och renderar ingenting i det förra fallet.
 *
 * <h3>Bara för en inloggad</h3>
 *
 * Läget kräver konto. Hämtningen är avstängd för en gäst, annars hade varje gäst som
 * öppnade en match kostat ett `401` och en väckt Render.
 */
export function useAttendanceState(matchId: string, enabled: boolean) {
  return useQuery({
    queryKey: attendanceStateQueryKey(matchId),
    queryFn: ({ signal }) => getAttendanceState(matchId, signal),
    enabled,
    staleTime: 0,
    retry: (failureCount, error) =>
      !(error instanceof ApiError && error.status === 404) && failureCount < 2,
  })
}
