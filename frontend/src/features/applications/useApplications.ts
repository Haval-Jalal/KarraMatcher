import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  apply,
  approveApplication,
  denyApplication,
  getApplications,
  getApplyInfo,
} from './applicationsApi'

/** Data och mutationer för ansökningar (§KM.3, `#194`). */

export const applicationKeys = {
  applyInfo: (truppId: string) => ['apply-info', truppId] as const,
  list: (truppId: string) => ['admin', 'applications', truppId] as const,
}

export function useApplyInfo(truppId: string) {
  return useQuery({
    queryKey: applicationKeys.applyInfo(truppId),
    queryFn: () => getApplyInfo(truppId),
    retry: false,
  })
}

export function useApply(truppId: string) {
  return useMutation({ mutationFn: () => apply(truppId) })
}

export function useApplications(truppId: string | null) {
  return useQuery({
    queryKey: applicationKeys.list(truppId ?? ''),
    queryFn: () => getApplications(truppId as string),
    enabled: truppId !== null,
  })
}

export function useApproveApplication(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => approveApplication(truppId, id),
    onSuccess: () => client.invalidateQueries({ queryKey: applicationKeys.list(truppId) }),
  })
}

export function useDenyApplication(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => denyApplication(truppId, id),
    onSuccess: () => client.invalidateQueries({ queryKey: applicationKeys.list(truppId) }),
  })
}
