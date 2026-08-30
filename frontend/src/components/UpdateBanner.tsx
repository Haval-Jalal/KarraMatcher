import { useEffect, useState } from 'react'

import { registerServiceWorker } from '@/lib/serviceWorker'

/**
 * Säger till när en ny version finns, och laddar om när användaren vill.
 *
 * Ingen ska fastna på gammal kod (Säkerhetschecklistan 5.5), men ingen ska heller få appen
 * utbytt mitt i en sida. `role="status"` gör att en skärmläsare får veta utan att bli
 * avbruten — det här är information, inte ett larm.
 */
export function UpdateBanner() {
  const [applyUpdate, setApplyUpdate] = useState<(() => void) | null>(null)

  useEffect(() => {
    registerServiceWorker({
      // Funktionen sparas i state, så den måste lindas — annars tolkar React den som en
      // uppdateringsfunktion och anropar den direkt.
      onUpdateReady: (apply) => {
        setApplyUpdate(() => apply)
      },
    })
  }, [])

  if (!applyUpdate) {
    return null
  }

  return (
    <div className="update-banner" role="status">
      <span>En ny version av appen finns.</span>
      <button type="button" className="button" onClick={applyUpdate}>
        Ladda om
      </button>
    </div>
  )
}
