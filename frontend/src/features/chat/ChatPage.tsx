import { useParams } from '@tanstack/react-router'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { accountIdFromToken } from '@/features/auth/authApi'
import { getAccessToken } from '@/lib/session'

import { ChatChannel } from './ChatChannel'
import { useMyTrupper } from './useChat'

/**
 * Trupp-chatten (§KM.1/§KM.10, `#201`).
 *
 * Bara medlemmar (server-side grind). En admin/tränare kan schemalägga; en admin ser också
 * anmälda meddelanden (trupp-övergripande, `#202`). Lag-kanalerna bor på `/lag/{slug}/chatt`.
 */
export function ChatPage() {
  const auth = useAuth()
  const params = useParams({ strict: false })
  const routeTruppId = typeof params.truppId === 'string' ? params.truppId : null

  const trupper = useMyTrupper()
  const [chosen, setChosen] = useState<string | null>(null)

  const options = trupper.data ?? []
  const truppId = chosen ?? routeTruppId ?? options[0]?.id ?? null
  const trupp = options.find((t) => t.id === truppId) ?? null

  const isAdmin = truppId !== null && (auth.isSuperAdmin || auth.adminOf.includes(truppId))
  const isLeader = trupp?.isLeader ?? false

  const token = getAccessToken()
  const myAccountId = token === null ? null : accountIdFromToken(token)

  return (
    <main className="page">
      <header className="app-header">
        <h1>Chatt</h1>
        {trupp !== null && (
          <p>
            {trupp.clubName} · {trupp.name} {trupp.season}
          </p>
        )}
      </header>

      {trupper.isLoading && <p className="state">Hämtar…</p>}
      {trupper.isError && (
        <p className="state state--error" role="alert">
          Kunde inte hämta dina trupper.
        </p>
      )}
      {trupper.data && options.length === 0 && (
        <p className="state">Du är inte medlem i någon trupp än.</p>
      )}

      {options.length > 1 && (
        <div className="form__field">
          <label htmlFor="chatt-valj-trupp">Välj trupp</label>
          <select
            id="chatt-valj-trupp"
            value={truppId ?? ''}
            onChange={(event) => setChosen(event.target.value)}
          >
            {options.map((t) => (
              <option key={t.id} value={t.id}>
                {t.clubName} · {t.name} {t.season}
              </option>
            ))}
          </select>
        </div>
      )}

      {truppId !== null && (
        <ChatChannel
          key={truppId}
          channel={{ kind: 'trupp', truppId }}
          isLeader={isLeader}
          isAdmin={isAdmin}
          myAccountId={myAccountId}
        />
      )}
    </main>
  )
}
