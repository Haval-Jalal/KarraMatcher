import { Link, useRouterState } from '@tanstack/react-router'
import type { ReactNode } from 'react'

/**
 * Vägen in för den som inte är inloggad (`#54`, §KM.3).
 *
 * <h3>En knapp som inte kan svika</h3>
 *
 * Gästen får se det mesta i appen men kan inte skriva. Den gränsen måste märkas *innan*
 * någon trycker: en knapp som ser vanlig ut och sedan svarar `401` läser en förälder som
 * att appen är trasig, och nästa gång skrivs det i föräldrachatten i stället. Därför är
 * det här en länk till inloggningen, aldrig en knapp som skickar ett anrop som ändå ska
 * nekas.
 *
 * <h3>Varför den bär med sig adressen</h3>
 *
 * `next` är sidan man stod på. Efter inloggningen hamnar man tillbaka där i stället för
 * på startsidan — den som var på väg att fråga om en plats ska inte behöva leta upp
 * matchen igen. `LoginPage` kastar bort allt som inte är en adress inuti appen, så värdet
 * kan aldrig bli en väg ut till någon annans webbplats.
 */
export function SignInLink({
  children,
  variant = 'primary',
}: {
  children: ReactNode
  /** `secondary` för det som står bredvid en viktigare åtgärd. */
  variant?: 'primary' | 'secondary'
}) {
  const pathname = useRouterState({ select: (state) => state.location.pathname })

  return (
    <Link
      to="/logga-in"
      search={{ next: pathname }}
      className={variant === 'primary' ? 'button' : 'button button--action'}
    >
      {children}
    </Link>
  )
}
