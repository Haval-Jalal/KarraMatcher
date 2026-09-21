import { useParams } from '@tanstack/react-router'

import { useAuth } from '@/features/auth'
import { accountIdFromToken } from '@/features/auth/authApi'
import { getAccessToken } from '@/lib/session'

import { ChatChannel } from './ChatChannel'
import { useTeamChatMeta } from './useChat'

/**
 * Lag-chatten (§KM.1/§KM.10, `#202`) — en egen kanal per färg-lag, på `/lag/{slug}/chatt`.
 *
 * Bara lagets medlemmar (server-side grind). Ledare (`meta.isLeader`) kan schemalägga; en
 * admin för truppen (via `adminOf`, truppens id från meta) kan radera vad som helst.
 * Anmälningskön finns bara på trupp-sidan — moderering delas mellan kanalerna.
 */
export function TeamChatPage() {
  const auth = useAuth()
  const { slug } = useParams({ from: '/lag/$slug/chatt' })
  const meta = useTeamChatMeta(slug)

  const isAdmin =
    meta.data !== undefined && (auth.isSuperAdmin || auth.adminOf.includes(meta.data.truppId))
  const isLeader = meta.data?.isLeader ?? false

  const token = getAccessToken()
  const myAccountId = token === null ? null : accountIdFromToken(token)

  return (
    <main className="page">
      <header className="app-header">
        <h1>Lagchatt</h1>
      </header>

      {meta.isLoading && <p className="state">Hämtar…</p>}
      {meta.isError && (
        <p className="state state--error" role="alert">
          Kunde inte öppna lagchatten.
        </p>
      )}

      {meta.data && (
        <ChatChannel
          channel={{ kind: 'team', slug }}
          isLeader={isLeader}
          isAdmin={isAdmin}
          myAccountId={myAccountId}
        />
      )}
    </main>
  )
}
