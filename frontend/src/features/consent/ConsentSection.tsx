import { useState } from 'react'

import { ApiError } from '@/lib/api'
import { formatFullDate } from '@/lib/time'

import { useCurrentConsent, useGrantConsent, useMyConsent } from './useConsent'

function messageOf(error: unknown): string {
  if (error instanceof ApiError) {
    return error.offline ? 'Ingen anslutning. Försök igen.' : error.message
  }

  return 'Något gick fel. Försök igen om en stund.'
}

/**
 * Vårdnadshavarens samtycke (§KM.6, `#195`), på Mitt konto.
 *
 * Visar den aktuella texten och en "Jag samtycker"-knapp tills man samtyckt, och därefter
 * vad man samtyckte till och när. Samtycket krävs innan ett barn kopplas (`#196`) — servern
 * är den riktiga grinden.
 */
export function ConsentSection() {
  const mine = useMyConsent()
  const current = useCurrentConsent()
  const grant = useGrantConsent()
  const [failure, setFailure] = useState<string | null>(null)

  return (
    <section aria-labelledby="samtycke-rubrik">
      <h2 className="match-list__title" id="samtycke-rubrik">
        Samtycke
      </h2>

      {(mine.isLoading || current.isLoading) && <p className="state">Hämtar…</p>}
      {(mine.isError || current.isError) && (
        <p className="state state--error" role="alert">
          Kunde inte hämta samtyckesinformationen.
        </p>
      )}

      {mine.data?.hasConsentedToCurrent && (
        <>
          <p className="state state--ok" role="status">
            Du har samtyckt till version {mine.data.version}
            {mine.data.grantedUtc !== null && <> den {formatFullDate(mine.data.grantedUtc)}</>}.
          </p>
          {mine.data.text !== null && (
            <details>
              <summary>Visa vad du samtyckte till</summary>
              <div className="consent-text">{mine.data.text}</div>
            </details>
          )}
        </>
      )}

      {mine.data && !mine.data.hasConsentedToCurrent && current.data && (
        <>
          <p>
            Innan ditt barn kan kopplas behöver du läsa och godkänna det här. Det gäller vad som
            sparas om barnet.
          </p>
          <div className="consent-text">{current.data.text}</div>

          {failure !== null && (
            <p className="state state--error" role="alert">
              {failure}
            </p>
          )}

          <div className="actions">
            <button
              type="button"
              className="button"
              disabled={grant.isPending}
              onClick={() => {
                setFailure(null)
                void grant
                  .mutateAsync(current.data.version)
                  .catch((error: unknown) => setFailure(messageOf(error)))
              }}
            >
              {grant.isPending ? 'Sparar…' : 'Jag samtycker'}
            </button>
          </div>
        </>
      )}
    </section>
  )
}
