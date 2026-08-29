import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render } from '@testing-library/react'

import { routeTree } from '@/app/routes'
import { SelectedTeamProvider } from '@/features/teams'

/**
 * Renderar appens riktiga routeträd på en given adress.
 *
 * Minneshistorik i stället för webbläsarens: klientsidig routing går inte att verifiera
 * med ett HTTP-anrop, eftersom servern svarar med index.html oavsett adress.
 *
 * Egen QueryClient per test, så att cachen aldrig läcker mellan testfall.
 */
export function renderRoute(initialPath: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  })

  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [initialPath] }),
  })

  const result = render(
    <QueryClientProvider client={queryClient}>
      <SelectedTeamProvider>
        <RouterProvider router={router} />
      </SelectedTeamProvider>
    </QueryClientProvider>,
  )

  return { ...result, router, queryClient }
}
