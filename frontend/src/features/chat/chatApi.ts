import { getAuthJson, postJson } from '@/lib/api'

/** Trupp-chatten (§KM.1/§KM.10, `#201`). Allt kräver inloggning; medlemskap prövas server-side. */

/** En trupp den inloggade är medlem av. `isLeader` = admin/tränare (får schemalägga). */
export interface MemberTrupp {
  id: string
  clubName: string
  name: string
  season: string
  isLeader: boolean
}

export interface ChatMessage {
  id: string
  authorAccountId: string
  authorName: string | null
  /** Texten, eller tom när meddelandet är borttaget. */
  body: string
  publishedUtc: string
  deleted: boolean
}

export interface ScheduledMessage {
  id: string
  body: string
  publishAtUtc: string
}

export interface ReportedMessage {
  messageId: string
  authorAccountId: string
  authorName: string | null
  body: string
  deleted: boolean
  reportCount: number
}

const base = (truppId: string) => `/api/v1/trupper/${encodeURIComponent(truppId)}/chat`

export const getMyTrupper = (signal?: AbortSignal): Promise<MemberTrupp[]> =>
  getAuthJson<MemberTrupp[]>('/api/v1/trupper/mina', signal)

export const getMessages = (truppId: string, signal?: AbortSignal): Promise<ChatMessage[]> =>
  getAuthJson<ChatMessage[]>(`${base(truppId)}/messages`, signal)

export const getScheduled = (truppId: string, signal?: AbortSignal): Promise<ScheduledMessage[]> =>
  getAuthJson<ScheduledMessage[]>(`${base(truppId)}/scheduled`, signal)

export const getReports = (truppId: string, signal?: AbortSignal): Promise<ReportedMessage[]> =>
  getAuthJson<ReportedMessage[]>(
    `/api/v1/admin/trupper/${encodeURIComponent(truppId)}/chat/reports`,
    signal,
  )

/** Postar nu, eller schemalägger om `publishAt` (ISO-tid i framtiden) anges. */
export const postMessage = (truppId: string, body: string, publishAt?: string): Promise<void> =>
  postJson<void>(
    `${base(truppId)}/messages`,
    publishAt === undefined ? { body } : { body, publishAt },
  )

export const deleteMessage = (truppId: string, id: string): Promise<void> =>
  postJson<void>(`${base(truppId)}/messages/${id}`, undefined, { method: 'DELETE' })

export const cancelScheduled = (truppId: string, id: string): Promise<void> =>
  postJson<void>(`${base(truppId)}/scheduled/${id}`, undefined, { method: 'DELETE' })

export const reportMessage = (truppId: string, id: string): Promise<void> =>
  postJson<void>(`${base(truppId)}/messages/${id}/report`)
