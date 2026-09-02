import { createContext } from 'react'

/**
 * Vem som är inloggad, om någon.
 *
 * <h3>Tre lägen, inte två</h3>
 *
 * `okänd` finns därför att appen inte vet något förrän den hunnit fråga. Utan det läget
 * hade en skyddad sida hunnit skicka en inloggad förälder till inloggningsrutan under den
 * halvsekund förnyelsen tar — vilket ser ut som att appen glömt bort en.
 */
export type AuthStatus = 'okand' | 'inloggad' | 'utloggad'

export interface AuthState {
  status: AuthStatus
  email: string | null
  /** Lagen man är tränare för. Styr vad som visas, aldrig vad som tillåts. */
  coachOf: string[]
  isAdmin: boolean
  /** Sant om den inloggade får sköta laget — tränare för det, eller administratör. */
  canManage: (teamSlug: string) => boolean
  /** Anropas när en inloggning just lyckats, så appen märker det direkt. */
  refresh: () => void
  signOut: () => Promise<void>
}

export const AuthContext = createContext<AuthState | null>(null)
