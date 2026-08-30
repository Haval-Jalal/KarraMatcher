import type { ReactNode } from 'react'

import { useSlowRequest } from '@/lib/useSlowRequest'

/**
 * Väntan, med en form att vänta mot.
 *
 * Två saker skiljer det här från en snurra. Formen antyder vad som kommer, så sidan inte
 * hoppar när innehållet landar. Och drar anropet ut på tiden säger appen varför — Render
 * sover efter en kvart och tar omkring 50 sekunder att vakna, vilket annars ser ut som
 * att appen hängt sig (§KM.11).
 *
 * Formen är `aria-hidden`. Grå rutor betyder ingenting för den som lyssnar, och skulle
 * bara bli brus mellan besked. Texten i `role="status"` är det som faktiskt sägs — och
 * `status` och inte `alert`, eftersom väntan inte är något man måste avbrytas för.
 */
export function LoadingState({
  label,
  isPending = true,
  children,
}: {
  /** Vad som hämtas, på svenska. Läses upp. */
  label: string
  /** Falskt bara i tester som vill se tillståndet utan att starta klockan. */
  isPending?: boolean
  /** Formen som visas medan man väntar. */
  children?: ReactNode
}) {
  const isSlow = useSlowRequest(isPending)

  return (
    <div className="loading">
      <p className="state" role="status">
        {label}
        {isSlow && (
          <span className="loading__slow">
            Servern har sovit och startar igen. Det kan ta upp till en minut.
          </span>
        )}
      </p>

      {children && (
        <div className="loading__shape" aria-hidden="true">
          {children}
        </div>
      )}
    </div>
  )
}
