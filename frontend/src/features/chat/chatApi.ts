import { getAuthJson, postJson } from '@/lib/api'

/** Chatten (§KM.1/§KM.10, `#201`/`#202`). Allt kräver inloggning; medlemskap prövas server-side. */

/**
 * En kanal: trupp-chatten (`#201`) eller ett lags kanal (`#202`). Samma meddelande-form och
 * samma anrop betjänar båda — bara bas-adressen skiljer.
 */
export type ChatChannel = { kind: 'trupp'; truppId: string } | { kind: 'team'; slug: string }

/** En trupp den inloggade är medlem av. `isLeader` = admin/tränare (får schemalägga). */
export interface MemberTrupp {
  id: string
  clubName: string
  name: string
  season: string
  isLeader: boolean
}

/** Meta om en lag-kanal: truppens id (för admin-kontroll via anspråk) och om jag är ledare. */
export interface TeamChatMeta {
  truppId: string
  isLeader: boolean
}

/**
 * En kanal i truppen som den inloggade får se (`#293`/`#298`). `kind` är `Trupp` för
 * primärkanalen ("{trupp} chatt") och `Team` för en lag-kanal ("Lag {färg} chatt"); servern
 * avgör vilka som kommer med. Färgen är null för primärkanalen.
 */
export interface ChatChannelSummary {
  kind: 'Trupp' | 'Team'
  teamId: string | null
  slug: string | null
  name: string
  colorHex: string | null
}

/** En reaktion på ett meddelande, aggregerad: emoji, antal och om jag själv reagerat (`#301`). */
export interface ChatReactionSummary {
  emoji: string
  count: number
  mine: boolean
}

export interface ChatMessage {
  id: string
  authorAccountId: string
  authorName: string | null
  /** Texten, eller tom när meddelandet är borttaget. */
  body: string
  publishedUtc: string
  deleted: boolean
  /** Reaktionerna per emoji (`#301`). Tom lista när inga finns. */
  reactions: ChatReactionSummary[]
}

/** De tillåtna reaktionerna — måste matcha serverns `ChatReaction.Allowed` (`#301`). */
export const REACTION_EMOJIS = ['👍', '❤️', '😂', '😮', '😢', '👏'] as const

/** Svenskt namn för en reaktion, för skärmläsare (aria-label). */
export const reactionLabel: Record<string, string> = {
  '👍': 'Tumme upp',
  '❤️': 'Hjärta',
  '😂': 'Skratt',
  '😮': 'Förvånad',
  '😢': 'Ledsen',
  '👏': 'Applåd',
}

export interface ScheduledMessage {
  id: string
  body: string
  publishAtUtc: string
}

/** En enskild anmälans motivering, så som admin ser den (`#263`). */
export interface ReportReason {
  reason: string
  reportedUtc: string
}

export interface ReportedMessage {
  messageId: string
  authorAccountId: string
  authorName: string | null
  body: string
  deleted: boolean
  reportCount: number
  reasons: ReportReason[]
}

/** En stabil nyckel per kanal, för query-cachen. */
export const channelKey = (channel: ChatChannel): string =>
  channel.kind === 'trupp' ? `trupp:${channel.truppId}` : `team:${channel.slug}`

const base = (channel: ChatChannel): string =>
  channel.kind === 'trupp'
    ? `/api/v1/trupper/${encodeURIComponent(channel.truppId)}/chat`
    : `/api/v1/teams/${encodeURIComponent(channel.slug)}/chat`

export const getMyTrupper = (signal?: AbortSignal): Promise<MemberTrupp[]> =>
  getAuthJson<MemberTrupp[]>('/api/v1/trupper/mina', signal)

/** Kanalerna den inloggade får se i truppen — primärkanalen först, sedan nåbara lag (`#293`). */
export const getChatChannels = (
  truppId: string,
  signal?: AbortSignal,
): Promise<ChatChannelSummary[]> =>
  getAuthJson<ChatChannelSummary[]>(
    `/api/v1/trupper/${encodeURIComponent(truppId)}/chat/channels`,
    signal,
  )

export const getTeamChatMeta = (slug: string, signal?: AbortSignal): Promise<TeamChatMeta> =>
  getAuthJson<TeamChatMeta>(`/api/v1/teams/${encodeURIComponent(slug)}/chat/meta`, signal)

export const getMessages = (channel: ChatChannel, signal?: AbortSignal): Promise<ChatMessage[]> =>
  getAuthJson<ChatMessage[]>(`${base(channel)}/messages`, signal)

export const getScheduled = (
  channel: ChatChannel,
  signal?: AbortSignal,
): Promise<ScheduledMessage[]> =>
  getAuthJson<ScheduledMessage[]>(`${base(channel)}/scheduled`, signal)

/** Anmälningskön är alltid på trupp-nivå (delad moderering, `#202`). */
export const getReports = (truppId: string, signal?: AbortSignal): Promise<ReportedMessage[]> =>
  getAuthJson<ReportedMessage[]>(
    `/api/v1/admin/trupper/${encodeURIComponent(truppId)}/chat/reports`,
    signal,
  )

/** Postar nu, eller schemalägger om `publishAt` (ISO-tid i framtiden) anges. */
export const postMessage = (
  channel: ChatChannel,
  body: string,
  publishAt?: string,
): Promise<void> =>
  postJson<void>(
    `${base(channel)}/messages`,
    publishAt === undefined ? { body } : { body, publishAt },
  )

export const deleteMessage = (channel: ChatChannel, id: string): Promise<void> =>
  postJson<void>(`${base(channel)}/messages/${id}`, undefined, { method: 'DELETE' })

/** Växlar min reaktion (emoji) på ett meddelande av och på (`#301`). */
export const toggleReaction = (channel: ChatChannel, id: string, emoji: string): Promise<void> =>
  postJson<void>(`${base(channel)}/messages/${id}/reactions`, { emoji })

export const cancelScheduled = (channel: ChatChannel, id: string): Promise<void> =>
  postJson<void>(`${base(channel)}/scheduled/${id}`, undefined, { method: 'DELETE' })

/** Anmäler ett meddelande med en obligatorisk motivering (`#263`). */
export const reportMessage = (channel: ChatChannel, id: string, reason: string): Promise<void> =>
  postJson<void>(`${base(channel)}/messages/${id}/report`, { reason })

/**
 * Adminens moderering: tar bort ett anmält meddelande oavsett kanal (`#202`). Kön spänner
 * hela truppen, så raderingen går via trupp-admin-endpointen — inte via medlemskanalen (som
 * nekar ett lag-meddelande).
 */
export const adminDeleteMessage = (truppId: string, id: string): Promise<void> =>
  postJson<void>(
    `/api/v1/admin/trupper/${encodeURIComponent(truppId)}/chat/messages/${id}`,
    undefined,
    { method: 'DELETE' },
  )
