/**
 * Klientfel loggas på ett enda ställe.
 *
 * Vi har ingen telemetri och ingen tredjepartsspårning (§KM.6) — det enda vi kan göra i
 * webbläsaren är att skriva till konsolen, så att ett fel går att se i en utvecklares eller
 * en tekniskt kunnig förälders devtools i stället för att försvinna spårlöst.
 *
 * Loggen får aldrig innehålla användarfritext eller barn-PII (§KM.10). Ett React-renderingsfel
 * bär utvecklarinformation (meddelande, stack) — inte innehållet användaren skrev — så själva
 * felobjektet är säkert att logga; skicka aldrig med formulärvärden eller liknande som extra data.
 */
export function logClientError(scope: string, error: unknown): void {
  console.error(`[${scope}]`, error)
}
