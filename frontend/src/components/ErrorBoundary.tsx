import { Component, type ErrorInfo, type ReactNode } from 'react'

import { logClientError } from '@/lib/logError'

interface Props {
  children: ReactNode
}

interface State {
  hasError: boolean
}

/**
 * Yttersta skyddsnätet mot en vit skärm (CLAUDE.md → Frontend, §KM.0 A3).
 *
 * <para>
 * Ett kast i en provider, i rot-layouten eller i en render utanför en route bubblar annars
 * hela vägen upp och lämnar användaren med en tom sida utan besked. Den här gränsen fångar
 * det, loggar felet (§KM.6/§KM.10 — ingen användartext) och visar ett lugnt svenskt fallback
 * med en väg vidare. Route-interna fel har sin egen gräns (`RouteError` via routerns
 * `defaultErrorComponent`); den här tar allt ovanför routern.
 * </para>
 *
 * En error boundary måste vara en klasskomponent — React fångar renderingsfel bara via
 * <c>getDerivedStateFromError</c>/<c>componentDidCatch</c>.
 */
export class ErrorBoundary extends Component<Props, State> {
  public override state: State = { hasError: false }

  public static getDerivedStateFromError(): State {
    return { hasError: true }
  }

  public override componentDidCatch(error: Error, info: ErrorInfo): void {
    logClientError('ErrorBoundary', error)
    logClientError('ErrorBoundary', info.componentStack ?? '')
  }

  public override render(): ReactNode {
    if (this.state.hasError) {
      return (
        <main className="error-fallback">
          <h1>Något gick fel</h1>
          <p>
            Appen råkade ut för ett oväntat fel. Prova att ladda om sidan — hjälper det inte, försök
            igen om en liten stund.
          </p>
          <button
            type="button"
            className="button button--action"
            onClick={() => window.location.reload()}
          >
            Ladda om sidan
          </button>
          <p>
            <a href="/">Till startsidan</a>
          </p>
        </main>
      )
    }

    return this.props.children
  }
}
