import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { ApiError } from '@/lib/api'

import {
  getKallelseSummary,
  getMyKallelse,
  remind,
  respond,
  setKallelse,
  type AttendanceReply,
} from './attendanceApi'

export const myKallelseQueryKey = (eventId: string) => ['kallelse', eventId, 'mine'] as const

export const kallelseSummaryQueryKey = (eventId: string) =>
  ['kallelse', eventId, 'summary'] as const

/**
 * Mina egna kallade barn för en händelse.
 *
 * <h3>404 är inte ett fel här</h3>
 *
 * Är kallelsen avslagen för laget svarar servern `404` (§KM.7) — ett giltigt svar, inte ett
 * fel. Hämtningen försöker inte igen på just det, och anroparen renderar ingenting.
 *
 * Bara för en inloggad; en gäst skulle bara kosta ett `401` och en väckt Render.
 */
export function useMyKallelse(eventId: string, enabled: boolean) {
  return useQuery({
    queryKey: myKallelseQueryKey(eventId),
    queryFn: ({ signal }) => getMyKallelse(eventId, signal),
    enabled,
    staleTime: 0,
    retry: (failureCount, error) =>
      !(error instanceof ApiError && error.status === 404) && failureCount < 2,
  })
}

/**
 * Adminens sammanställning för en händelse.
 *
 * Aktiveras bara för en admin — annars svarar anropet ändå 403/404. Kort färskhet: svar
 * trillar in under dagarna före.
 */
export function useKallelseSummary(truppId: string, eventId: string, enabled: boolean) {
  return useQuery({
    queryKey: kallelseSummaryQueryKey(eventId),
    queryFn: ({ signal }) => getKallelseSummary(truppId, eventId, signal),
    enabled,
    staleTime: 0,
  })
}

/** Sparar en vårdnadshavares Ja/Nej för ett barn och läser om både min vy och summeringen. */
export function useRespond(eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { childId: string; reply: AttendanceReply }) =>
      respond(eventId, input.childId, input.reply),
    onSuccess: () => invalidate(client, eventId),
  })
}

/** Skickar/uppdaterar kallelsen (adminens barn-urval) och läser om summeringen och min vy. */
export function useSetKallelse(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (childIds: string[]) => setKallelse(truppId, eventId, childIds),
    onSuccess: () => invalidate(client, eventId),
  })
}

/** Påminner dem som inte svarat och läser om summeringen. */
export function useRemind(truppId: string, eventId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: () => remind(truppId, eventId),
    onSuccess: () => invalidate(client, eventId),
  })
}

async function invalidate(
  client: ReturnType<typeof useQueryClient>,
  eventId: string,
): Promise<void> {
  await Promise.all([
    client.invalidateQueries({ queryKey: myKallelseQueryKey(eventId) }),
    client.invalidateQueries({ queryKey: kallelseSummaryQueryKey(eventId) }),
  ])
}
