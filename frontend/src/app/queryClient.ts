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
      // Behåll globala omförsök. På sidor utan `requireSession` (t.ex. /spelarkort) kan ett
      // första anrop få 401 innan sessionen hunnit förnyas i minnet; omförsöket är det som
      // låter det lyckas när token väl finns. Enskilda queries som vet att sessionen redan är
      // säkrad (t.ex. useEvent) väljer själva att inte göra om ett 4xx.
      retry: 2,
    },
  },
})
