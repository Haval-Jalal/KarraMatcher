import { useId, useRef, useState } from 'react'

import { accountIdFromToken } from '@/features/auth/authApi'
import { getAccessToken } from '@/lib/session'

import { ChatChannel } from './ChatChannel'
import type { ChatChannel as Channel, ChatChannelSummary } from './chatApi'
import { useChatChannels } from './useChat'

/** En stabil nyckel per kanal-rad, för val och tab-id. */
const keyOf = (channel: ChatChannelSummary): string =>
  channel.kind === 'Trupp' ? 'trupp' : `team:${channel.slug ?? ''}`

/** Kanal-summeringen från listan → adressen `ChatChannel` postar/läser mot. */
function toChannel(truppId: string, channel: ChatChannelSummary): Channel {
  return channel.kind === 'Trupp'
    ? { kind: 'trupp', truppId }
    : { kind: 'team', slug: channel.slug ?? '' }
}

/**
 * En trupps chatt som en samlad vy med kanalväxlare (`#294`): primärkanalen ("{trupp} chatt")
 * och de lag-kanaler den inloggade når, alla på ett ställe. Kanalerna kommer server-beräknade
 * från `#293` — FE avgör aldrig själv vem som får se vad.
 *
 * <para>
 * Ledarskap och admin är trupp-breda (server: <c>IsLeaderOfTruppAsync</c>), så samma
 * <paramref name="isLeader"/>/<paramref name="isAdmin"/> gäller alla truppens kanaler — därför
 * behövs ingen per-lag-meta här. Växlaren är en riktig tablist för tangentbordet; färgen är ett
 * tillägg till namnet, aldrig enda signalen (WCAG 1.4.1).
 * </para>
 */
export function ChatView({
  truppId,
  isLeader,
  isAdmin,
  initialTeamSlug = null,
}: {
  truppId: string
  isLeader: boolean
  isAdmin: boolean
  initialTeamSlug?: string | null
}) {
  const channels = useChatChannels(truppId)
  const [chosenKey, setChosenKey] = useState<string | null>(
    initialTeamSlug !== null ? `team:${initialTeamSlug}` : null,
  )
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([])
  const baseId = useId()

  const token = getAccessToken()
  const myAccountId = token === null ? null : accountIdFromToken(token)

  if (channels.isPending) {
    return (
      <p className="state" role="status">
        Hämtar kanaler…
      </p>
    )
  }

  if (channels.isError) {
    return (
      <p className="state state--error" role="alert">
        Kunde inte hämta kanalerna.
      </p>
    )
  }

  const list = channels.data
  // Primärkanalen finns alltid; om inget lag valts (eller det valda inte nås) → första kanalen.
  const selected = list.find((channel) => keyOf(channel) === chosenKey) ?? list[0] ?? null

  if (selected === null) {
    return <p className="state">Den här truppen har ingen chatt ännu.</p>
  }

  const selectedKey = keyOf(selected)

  function moveTo(index: number): void {
    const channel = list[index]
    if (channel === undefined) {
      return
    }

    setChosenKey(keyOf(channel))
    tabRefs.current[index]?.focus()
  }

  function onKeyDown(event: React.KeyboardEvent, index: number): void {
    const last = list.length - 1
    let next: number | null = null

    if (event.key === 'ArrowRight') next = index === last ? 0 : index + 1
    else if (event.key === 'ArrowLeft') next = index === 0 ? last : index - 1
    else if (event.key === 'Home') next = 0
    else if (event.key === 'End') next = last

    if (next === null) {
      return
    }

    event.preventDefault()
    moveTo(next)
  }

  return (
    <>
      {list.length > 1 && (
        <div role="tablist" aria-label="Kanaler" className="chat-tabs">
          {list.map((channel, index) => {
            const key = keyOf(channel)
            const active = key === selectedKey

            return (
              <button
                key={key}
                ref={(element) => {
                  tabRefs.current[index] = element
                }}
                type="button"
                role="tab"
                id={`${baseId}-tab-${key}`}
                aria-selected={active}
                aria-controls={`${baseId}-panel`}
                tabIndex={active ? 0 : -1}
                className={active ? 'chat-tabs__tab chat-tabs__tab--active' : 'chat-tabs__tab'}
                style={
                  active && channel.colorHex !== null
                    ? { borderBottomColor: channel.colorHex }
                    : undefined
                }
                onClick={() => setChosenKey(key)}
                onKeyDown={(event) => onKeyDown(event, index)}
              >
                {channel.colorHex !== null && (
                  <span
                    className="chat-tabs__dot"
                    style={{ background: channel.colorHex }}
                    aria-hidden="true"
                  />
                )}
                {channel.name}
              </button>
            )
          })}
        </div>
      )}

      <div
        role="tabpanel"
        id={`${baseId}-panel`}
        aria-labelledby={`${baseId}-tab-${selectedKey}`}
        tabIndex={0}
        className="chat-panel"
      >
        <ChatChannel
          key={selectedKey}
          channel={toChannel(truppId, selected)}
          isLeader={isLeader}
          isAdmin={isAdmin}
          myAccountId={myAccountId}
        />
      </div>
    </>
  )
}
