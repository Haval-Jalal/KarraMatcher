import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'

import { renewSession } from '@/lib/api'
import { getAccessToken } from '@/lib/session'

import {
  adminTruppFromToken,
  coachTeamsFromToken,
  emailFromToken,
  isAdminFromToken,
  isSuperAdminFromToken,
  signOut as signOutRequest,
} from './authApi'
import { AuthContext, type AuthState, type AuthStatus } from './authContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('okand')
  const [email, setEmail] = useState<string | null>(null)
  const [coachOf, setCoachOf] = useState<string[]>([])
  const [isAdmin, setIsAdmin] = useState(false)
  const [isSuperAdmin, setIsSuperAdmin] = useState(false)
  const [adminOf, setAdminOf] = useState<string[]>([])

  const read = useCallback(() => {
    const token = getAccessToken()

    setStatus(token === null ? 'utloggad' : 'inloggad')
    setEmail(token === null ? null : emailFromToken(token))
    setCoachOf(token === null ? [] : coachTeamsFromToken(token))
    setIsAdmin(token === null ? false : isAdminFromToken(token))
    setIsSuperAdmin(token === null ? false : isSuperAdminFromToken(token))
    setAdminOf(token === null ? [] : adminTruppFromToken(token))
  }, [])

  useEffect(() => {
    let cancelled = false

    /*
     * Vid kallstart forsoker appen forlanga sessionen mot refresh-cookien (§KM.11). Ingen
     * localStorage-ledtrad langre (`#255`): iOS gallrar skrivbar lagring men inte den
     * httpOnly cookien, sa en installerad app ska kunna logga in sig sjalv fran cookien
     * aven efter att lagringen rensats. I stangda v2 finns inga anonyma besokare, sa det
     * extra anropet for en gast (ett 401) ar ofarligt.
     *
     * Bara nar access-token saknas: har route-vakten redan fornyat racker det, och en ny
     * fornyelse hade roterat refresh-token i onodan.
     */
    async function restore() {
      if (getAccessToken() === null) {
        await renewSession()
      }

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
      coachOf,
      isAdmin,
      isSuperAdmin,
      adminOf,
      canManage: (teamSlug: string) => isAdmin || coachOf.includes(teamSlug),
      refresh: read,
      signOut: async () => {
        await signOutRequest()
        read()
      },
    }),
    [status, email, coachOf, isAdmin, isSuperAdmin, adminOf, read],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
