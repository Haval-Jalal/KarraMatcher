import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from '@tanstack/react-router'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'

import { SelectedTeamProvider } from '@/features/teams'

/**
 * Renderar med samma providerkedja som appen: TanStack Query, valt lag, och en minimal
 * router — matchkorten är länkar, och `Link` kräver en routerkontext.
 *
 * En egen QueryClient per test, så att cachen aldrig läcker mellan testfall — annars hade
 * ett test kunnat se data som ett tidigare test hämtade och gå grönt av fel skäl.
 * `retry: false` gör att ett feltillstånd syns direkt i stället för efter tre försök.
 *
 * Routern laddas före render. Utan det monterar den asynkront och testerna skulle behöva
 * `findBy` även på det som inte hämtas.
 */
export async function renderWithProviders(ui: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  })

  const rootRoute = createRootRoute({ component: () => <Outlet /> })
  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    component: () => <>{ui}</>,
  })

  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
  })

  await router.load()

  const result = render(
    <QueryClientProvider client={queryClient}>
      <SelectedTeamProvider>
        <RouterProvider router={router as never} />
      </SelectedTeamProvider>
    </QueryClientProvider>,
  )

  return { ...result, queryClient }
}
