import { SLOW_REQUEST_MESSAGE, useSlowRequest } from '@/lib/useSlowRequest'

/**
 * Kallstarts-beskedet för en SKRIVNING (`#591`, §KM.11).
 *
 * `useSlowRequest` (samma mätning som sid-laddningen använder via LoadingState) satt förut bara
 * på hämtningar. En skrivning — svara på kallelse, skicka kallelse, påminna, lägga upp/fråga om
 * samåkning, skicka ett chattmeddelande — visade bara en inaktiverad knapp. Första trycket efter
 * en tyst natt kunde hänga ~50 s utan förklaring, precis det "ser trasigt ut" §KM.11 finns för.
 *
 * Släpp in den här nära submit-kontrollen med mutationens `isPending`. Den säger ingenting förrän
 * tröskeln passeras; sedan annonserar `role="status"` beskedet utan att avbryta (det är väntan,
 * inte ett fel).
 */
export function SlowRequestNotice({ isPending }: { isPending: boolean }) {
  const isSlow = useSlowRequest(isPending)

  if (!isSlow) {
    return null
  }

  return (
    <p className="state loading__slow" role="status">
      {SLOW_REQUEST_MESSAGE}
    </p>
  )
}
