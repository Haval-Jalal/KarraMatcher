import { type ErrorComponentProps, Link } from '@tanstack/react-router'
import { useEffect } from 'react'

import { logClientError } from '@/lib/logError'
import { useDocumentTitle } from '@/lib/useDocumentTitle'

/**
 * Fallback för ett fel som kastas *inne i* en route (loader eller komponent). Sätts som
 * routerns <c>defaultErrorComponent</c> så att TanStacks engelska, ologgade standard aldrig
 * visas — texten är svensk (§KM.9) och felet loggas (§KM.6/§KM.10, ingen användartext).
 *
 * Till skillnad från <c>ErrorBoundary</c> lever routern här, så vi kan erbjuda både en
 * riktig retry (<paramref name="reset"/>) och en typsäker länk tillbaka till startsidan.
 */
export function RouteError({ error, reset }: ErrorComponentProps) {
  useDocumentTitle('Något gick fel')

  useEffect(() => {
    logClientError('RouteError', error)
  }, [error])

  return (
    <main className="error-fallback">
      <h1>Något gick fel</h1>
      <p>Vi kunde inte visa sidan just nu. Försök igen — kvarstår felet, ladda om appen.</p>
      <button type="button" className="button button--action" onClick={reset}>
        Försök igen
      </button>
      <p>
        <Link to="/">Till startsidan</Link>
      </p>
    </main>
  )
}
