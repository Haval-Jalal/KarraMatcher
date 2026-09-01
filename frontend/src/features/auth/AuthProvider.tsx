import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'

import { renewSession } from '@/lib/api'
import { getAccessToken, hasSessionHint } from '@/lib/session'

import { emailFromToken, signOut as signOutRequest } from './authApi'
import { AuthContext, type AuthState, type AuthStatus } from './authContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('okand')
  const [email, setEmail] = useState<string | null>(null)

  const read = useCallback(() => {
    const token = getAccessToken()

    setStatus(token === null ? 'utloggad' : 'inloggad')
    setEmail(token === null ? null : emailFromToken(token))
  }, [])

  useEffect(() => {
    let cancelled = false

    /*
     * Vid start forsoker appen forlanga sessionen mot cookien -- men bara om nagon
     * faktiskt loggat in har tidigare.
     *
     * Utan den kontrollen hade varje besok gjort ett anrop mot inloggningen, aven for de
     * allra flesta som aldrig loggar in. Det anropet gar inte att cacha pa Vercels edge
     * och skulle darfor vacka Render varje gang nagon oppnar schemat -- precis de femtio
     * sekunderna §KM.11 finns till for att slippa.
     */
    async function restore() {
      if (!hasSessionHint()) {
        if (!cancelled) {
          setStatus('utloggad')
        }

        return
      }

      await renewSession()

      if (!cancelled) {
        read()
      }
    }

    void restore()

    return () => {
      cancelled = true
    }
  }, [read])

  const value = useMemo<AuthState>(
    () => ({
      status,
      email,
      refresh: read,
      signOut: async () => {
        await signOutRequest()
        read()
      },
    }),
    [status, email, read],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
