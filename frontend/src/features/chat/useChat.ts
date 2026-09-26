import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  cancelScheduled,
  channelKey,
  adminDeleteMessage,
  deleteMessage,
  getChatChannels,
  getMessages,
  getMyTrupper,
  getReports,
  getScheduled,
  getTeamChatMeta,
  postMessage,
  reportMessage,
  toggleReaction,
  type ChatChannel,
} from './chatApi'

/** Data och mutationer för chatten (§KM.1, `#201`/`#202`). */

export const chatKeys = {
  myTrupper: ['chat', 'mina-trupper'] as const,
  channels: (truppId: string) => ['chat', 'channels', truppId] as const,
  teamMeta: (slug: string) => ['chat', 'team-meta', slug] as const,
  messages: (channel: ChatChannel) => ['chat', 'messages', channelKey(channel)] as const,
  scheduled: (channel: ChatChannel) => ['chat', 'scheduled', channelKey(channel)] as const,
  reports: (truppId: string) => ['chat', 'reports', truppId] as const,
}

export function useMyTrupper() {
  return useQuery({
    queryKey: chatKeys.myTrupper,
    queryFn: ({ signal }) => getMyTrupper(signal),
  })
}

export function useChatChannels(truppId: string | null) {
  return useQuery({
    queryKey: chatKeys.channels(truppId ?? ''),
    queryFn: ({ signal }) => getChatChannels(truppId as string, signal),
    enabled: truppId !== null,
  })
}

export function useTeamChatMeta(slug: string) {
  return useQuery({
    queryKey: chatKeys.teamMeta(slug),
    queryFn: ({ signal }) => getTeamChatMeta(slug, signal),
  })
}

export function useChatMessages(channel: ChatChannel) {
  return useQuery({
    queryKey: chatKeys.messages(channel),
    queryFn: ({ signal }) => getMessages(channel, signal),
  })
}

export function useScheduled(channel: ChatChannel, enabled: boolean) {
  return useQuery({
    queryKey: chatKeys.scheduled(channel),
    queryFn: ({ signal }) => getScheduled(channel, signal),
    enabled,
  })
}

/** Anmälningskön är alltid på trupp-nivå (delad moderering). */
export function useReports(truppId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: chatKeys.reports(truppId ?? ''),
    queryFn: ({ signal }) => getReports(truppId as string, signal),
    enabled: truppId !== null && enabled,
  })
}

export function usePost(channel: ChatChannel) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { body: string; publishAt?: string }) =>
      postMessage(channel, input.body, input.publishAt),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: chatKeys.messages(channel) })
      void client.invalidateQueries({ queryKey: chatKeys.scheduled(channel) })
    },
  })
}

export function useDeleteMessage(channel: ChatChannel) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteMessage(channel, id),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: chatKeys.messages(channel) })

      // Adminens anmälningskö lever på trupp-nivå; håll den i synk när en radering sker där.
      if (channel.kind === 'trupp') {
        void client.invalidateQueries({ queryKey: chatKeys.reports(channel.truppId) })
      }
    },
  })
}

export function useAdminDeleteMessage(truppId: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminDeleteMessage(truppId, id),

    // Det borttagna meddelandet kan ligga i vilken kanal som helst — uppdatera hela chatten.
    onSuccess: () => client.invalidateQueries({ queryKey: ['chat'] }),
  })
}

export function useCancelScheduled(channel: ChatChannel) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => cancelScheduled(channel, id),
    onSuccess: () => client.invalidateQueries({ queryKey: chatKeys.scheduled(channel) }),
  })
}

export function useToggleReaction(channel: ChatChannel) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { messageId: string; emoji: string }) =>
      toggleReaction(channel, input.messageId, input.emoji),
    onSuccess: () => client.invalidateQueries({ queryKey: chatKeys.messages(channel) }),
  })
}

export function useReport(channel: ChatChannel) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: { id: string; reason: string }) =>
      reportMessage(channel, input.id, input.reason),
    onSuccess: () => {
      // Admins anmälningskö lever på trupp-nivå; håll den i synk om anmälaren ser den.
      if (channel.kind === 'trupp') {
        void client.invalidateQueries({ queryKey: chatKeys.reports(channel.truppId) })
      }
    },
  })
}
