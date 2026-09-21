import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  cancelScheduled,
  deleteMessage,
  getMessages,
  getMyTrupper,
  getReports,
  getScheduled,
  postMessage,
  reportMessage,
} from './chatApi'

/** Data och mutationer för trupp-chatten (§KM.1, `#201`). */

export const chatKeys = {
  myTrupper: ['chat', 'mina-trupper'] as const,
  messages: (truppId: string) => ['chat', 'messages', truppId] as const,
  scheduled: (truppId: string) => ['chat', 'scheduled', truppId] as const,
  reports: (truppId: string) => ['chat', 'reports', truppId] as const,
}

export function useMyTrupper() {
  return useQuery({
    queryKey: chatKeys.myTrupper,
    queryFn: ({ signal }) => getMyTrupper(signal),
  })
}

export function useChatMessages(truppId: string | null) {
  return useQuery({
    queryKey: chatKeys.messages(truppId ?? ''),
    queryFn: ({ signal }) => getMessages(truppId as string, signal),
    enabled: truppId !== null,
  })
}

export function useScheduled(truppId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: chatKeys.scheduled(truppId ?? ''),
    queryFn: ({ signal }) => getScheduled(truppId as string, signal),
    enabled: truppId !== null && enabled,
  })
}

export function useReports(truppId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: chatKeys.reports(truppId ?? ''),
    queryFn: ({ signal }) => getReports(truppId as string, signal),
    enabled: truppId !== null && enabled,
  })
}

export function usePost(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { body: string; publishAt?: string }) =>
      postMessage(truppId, input.body, input.publishAt),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: chatKeys.messages(truppId) })
      void client.invalidateQueries({ queryKey: chatKeys.scheduled(truppId) })
    },
  })
}

export function useDeleteMessage(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteMessage(truppId, id),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: chatKeys.messages(truppId) })
      void client.invalidateQueries({ queryKey: chatKeys.reports(truppId) })
    },
  })
}

export function useCancelScheduled(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => cancelScheduled(truppId, id),
    onSuccess: () => client.invalidateQueries({ queryKey: chatKeys.scheduled(truppId) }),
  })
}

export function useReport(truppId: string) {
  return useMutation({
    mutationFn: (id: string) => reportMessage(truppId, id),
  })
}
