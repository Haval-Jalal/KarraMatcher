import { QueryClient } from '@tanstack/react-query'

import { ApiError } from '@/lib/api'

/**
 * Server-state hanteras av TanStack Query, aldrig av useEffect-fetch.
 * Se CLAUDE.md → Frontend, Datalager & navigation.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Matchschemat ändras sällan. Att slippa hämta om vid varje fönsterfokus
      // sparar mobildata och håller appen tyst på dålig täckning.
      refetchOnWindowFocus: false,
      staleTime: 60_000,
      // Ett klientfel (4xx) blir aldrig rätt av att försökas igen — ett 403/404 är samma svar
      // varje gång. Att ändå göra tre försök väcker Render i onödan (§KM.11) och fördröjer
      // felbeskedet. Nätverks-/serverfel (inkl. offline, status 0) försöks fortfarande om.
      retry: (failureCount, error) => {
        if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
          return false
        }

        return failureCount < 2
      },
    },
  },
})
