import { useContext } from 'react'

import { AuthContext, type AuthState } from './authContext'

export function useAuth(): AuthState {
  const context = useContext(AuthContext)

  if (context === null) {
    throw new Error('useAuth måste användas inuti AuthProvider.')
  }

  return context
}
