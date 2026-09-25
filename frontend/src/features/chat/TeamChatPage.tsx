import { useParams } from '@tanstack/react-router'

import { useAuth } from '@/features/auth'

import { ChatView } from './ChatView'
import { useTeamChatMeta } from './useChat'

/**
 * Lag-chattens djuplänk (`/lag/{slug}/chatt`, `#202`) — samma samlade vy som `/chatt`, men med
 * det här laget förvalt i kanalväxlaren (`#294`). Nås kontextuellt från lag-sidan.
 *
 * Bara lagets medlemmar (server-side grind). Truppens id kommer ur lag-metan; ledarskap och
 * admin är trupp-breda och gäller därför alla truppens kanaler. Anmälningskön (delad moderering)
 * visar sig bara på trupp-kanalen, inuti `ChatView`.
 */
export function TeamChatPage() {
  const auth = useAuth()
  const { slug } = useParams({ from: '/lag/$slug/chatt' })
  const meta = useTeamChatMeta(slug)

  const isAdmin =
    meta.data !== undefined && (auth.isSuperAdmin || auth.adminOf.includes(meta.data.truppId))
  const isLeader = meta.data?.isLeader ?? false

  return (
    <main className="page">
      <header className="app-header">
        <h1>Chatt</h1>
      </header>

      {meta.isLoading && <p className="state">Hämtar…</p>}
      {meta.isError && (
        <p className="state state--error" role="alert">
          Kunde inte öppna lagchatten.
        </p>
      )}

      {meta.data && (
        <ChatView
          truppId={meta.data.truppId}
          isLeader={isLeader}
          isAdmin={isAdmin}
          initialTeamSlug={slug}
        />
      )}
    </main>
  )
}
