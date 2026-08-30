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

/**
 * Renderar en komponent i en minimal router.
 *
 * Matchkorten är länkar, och `Link` kräver en routerkontext. Att dra in appens hela
 * routeträd för ett komponenttest hade kopplat testet till sidor det inte handlar om —
 * här räcker en rot med en indexrutt som renderar just den komponent som prövas.
 *
 * Routern laddas före render. Utan det monterar den asynkront och testet skulle behöva
 * `findBy` på varje assertion — ett `await` här håller själva testerna synkrona.
 */
export async function renderWithRouter(ui: ReactNode) {
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

  return render(<RouterProvider router={router as never} />)
}
