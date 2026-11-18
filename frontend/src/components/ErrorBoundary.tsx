import { Component, createRef, type ErrorInfo, type ReactNode } from 'react'

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

  // Fallbacken sitter ovanför RootLayout, så `#innehall`-fokuseffekten kör inte här. Utan en egen
  // fokusflytt lämnas en skärmläsar-användare kvar i den trasiga vyn utan att få veta något.
  private readonly headingRef = createRef<HTMLHeadingElement>()

  public static getDerivedStateFromError(): State {
    return { hasError: true }
  }

  public override componentDidCatch(error: Error, info: ErrorInfo): void {
    logClientError('ErrorBoundary', error)
    logClientError('ErrorBoundary', info.componentStack ?? '')
  }

  public override componentDidMount(): void {
    // Ett kast redan under första renderingen gör fallbacken till gränsens första commit — det är
    // ett mount, inte en update. Fokusera rubriken även då.
    if (this.state.hasError) {
      this.headingRef.current?.focus()
    }
  }

  public override componentDidUpdate(_prevProps: Props, prevState: State): void {
    // Ett kast efter att gränsen monterats: flytta fokus till rubriken så den annonseras och
    // tangentbordet står i fallbacken, inte kvar i det som kraschade (WCAG 2.4.3).
    if (this.state.hasError && !prevState.hasError) {
      this.headingRef.current?.focus()
    }
  }

  public override render(): ReactNode {
    if (this.state.hasError) {
      return (
        <main className="error-fallback" role="alert">
          <h1 ref={this.headingRef} tabIndex={-1}>
            Något gick fel
          </h1>
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
