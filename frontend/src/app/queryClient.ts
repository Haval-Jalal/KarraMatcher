import { QueryClient } from '@tanstack/react-query'

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
      retry: 2,
    },
  },
})
