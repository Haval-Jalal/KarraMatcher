import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'

import { getAddressSuggestions } from './addressApi'

/** Kortare än så är inte värt ett anrop — samma tröskel som backend (PhotonAddressSuggester). */
const MIN_LENGTH = 3

/** En paus efter sista tangenttrycket, så vi inte anropar per bokstav (fair use + rate limit). */
const DEBOUNCE_MS = 300

/**
 * Adress-förslag för "annan plats" i händelseformuläret (`#307`).
 *
 * <h3>Debounce och tröskel</h3>
 *
 * Förslagen hämtas först när skrivandet lugnat sig ({@link DEBOUNCE_MS} ms) och termen är minst
 * {@link MIN_LENGTH} tecken. Det håller anropen mot Photon låga — resten sköter backend och den
 * globala rate-limitern.
 *
 * <h3>Offline och tomt</h3>
 *
 * Ett misslyckat anrop ger inga förslag (och inget fel som stör skrivandet) — fältet fungerar
 * som vanlig fritext, vilket är hela fallbacken. Tomt/för kort term ger en tom lista utan anrop.
 */
export function useAddressSuggestions(term: string): string[] {
  const debounced = useDebouncedValue(term.trim(), DEBOUNCE_MS)
  const enabled = debounced.length >= MIN_LENGTH

  const query = useQuery({
    queryKey: ['address-suggestions', debounced],
    queryFn: ({ signal }) => getAddressSuggestions(debounced, signal),
    enabled,
    // Förslagen ändras inte mellan tangenttryck på samma term; behåll dem medan nästa hämtas.
    placeholderData: keepPreviousData,
    staleTime: 5 * 60 * 1000,
  })

  // Array.isArray och inte bara ?? []: ett oväntat svar (fel form, en proxy-sida) får aldrig
  // krascha datalisten — då blir det inga förslag, och fältet är kvar som fritext.
  return enabled && Array.isArray(query.data) ? query.data : []
}

/** Fördröjer ett värde tills det stått stilla en stund. Städar timern vid varje ändring. */
function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs)

    return () => clearTimeout(timer)
  }, [value, delayMs])

  return debounced
}
