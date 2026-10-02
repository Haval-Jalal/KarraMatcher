import { useEffect, useRef } from 'react'

/**
 * Flyttar fokus till ett element när det monteras (dvs. när en panel/formulär fälls in).
 *
 * <h3>Varför</h3>
 *
 * När en knapp som öppnar en panel ersätts av panelen försvinner den fokuserade knappen ur DOM:en,
 * och fokus faller till `<body>` — en tangentbords- eller skärmläsaranvändare släpps då till sidans
 * topp utan besked (WCAG 2.4.3, `#598`). Fäst den returnerade ref:en på panelens naturliga start
 * (första fältet, eller en rubrik med `tabIndex={-1}`) så fokus följer med in.
 *
 * Ett ensamt beroende på <c>active</c>: fokus flyttas när värdet ändras (panelen blir aktiv, eller
 * byter vilket objekt den gäller), inte vid varje render. Ett sanningsvärde (t.ex. ett öppet-id)
 * duger lika bra som en boolean.
 */
export function useAutoFocus<T extends HTMLElement>(active: unknown = true) {
  const ref = useRef<T>(null)

  useEffect(() => {
    if (active) {
      ref.current?.focus()
    }
  }, [active])

  return ref
}
