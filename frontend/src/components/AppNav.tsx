import { Link, useNavigate, useRouterState } from '@tanstack/react-router'
import { useCallback, useEffect, useId, useRef, useState } from 'react'

import { useAuth } from '@/features/auth'

/**
 * Appens huvudmeny (`#148`, omgjord till hamburgmeny i `#273`).
 *
 * <h3>En lugn topbar + en drawer</h3>
 *
 * Den gamla länkraden växte för varje roll. I den varma Andrum-designen bor navigeringen i
 * stället bakom en hamburgare: en tyst topbar (knapp + ordmärket "Truppen") och en drawer som
 * glider in. Drawern är en riktig dialog för tangentbordet — fokus flyttas in när den öppnas
 * och tillbaka till knappen när den stängs, Esc och överlägget stänger, och `inert` tar bort
 * den ur både tabbordning och skärmläsare när den är stängd.
 *
 * <h3>Menyn visar, den tillåter inget</h3>
 *
 * Tränarlänken syns bara för en tränare, men det är ett bekvämlighetsval och ingen
 * säkerhetsgräns — auktoriseringen ligger i backend. Samma sak för Admin och Superadmin.
 */
export function AppNav() {
  const { status, coachOf, isSuperAdmin, adminOf, signOut } = useAuth()
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const navigate = useNavigate()

  const [open, setOpen] = useState(false)
  const panelRef = useRef<HTMLElement>(null)
  const burgerRef = useRef<HTMLButtonElement>(null)
  const wasOpen = useRef(false)
  const panelId = useId()

  const close = useCallback(() => setOpen(false), [])

  // Medan menyn är öppen: fånga fokus i drawern, stäng på Esc, lås bakgrundens skroll.
  useEffect(() => {
    if (!open) {
      return
    }

    const panel = panelRef.current
    const focusables = (): HTMLElement[] =>
      panel ? [...panel.querySelectorAll<HTMLElement>('a[href], button:not([disabled])')] : []

    focusables()[0]?.focus()

    function onKeyDown(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        event.preventDefault()
        setOpen(false)
        return
      }

      if (event.key !== 'Tab') {
        return
      }

      const items = focusables()

      if (items.length === 0) {
        return
      }

      const first = items[0]!
      const last = items[items.length - 1]!

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', onKeyDown, true)
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    return () => {
      document.removeEventListener('keydown', onKeyDown, true)
      document.body.style.overflow = previousOverflow
    }
  }, [open])

  // När menyn stängts (efter att ha varit öppen) återlämnas fokus till hamburgaren.
  useEffect(() => {
    if (wasOpen.current && !open) {
      burgerRef.current?.focus()
    }

    wasOpen.current = open
  }, [open])

  const teamInPath = /^\/lag\/([^/]+)/.exec(pathname)?.[1] ?? null

  /*
   * Tranarlanken pekar pa det lag man tittar pa om man skoter det, annars pa det forsta laget
   * man ar tranare for. Vilket lag den pekar pa far bero pa sidan -- men *om* den syns styr
   * rollen ensam (annars bytte menyn innehall nar man klickade runt, `#253`).
   */
  const coachTeam =
    teamInPath !== null && coachOf.includes(teamInPath) ? teamInPath : (coachOf[0] ?? null)

  const current = sectionOf(pathname)
  const loggedIn = status === 'inloggad'

  async function handleSignOut(): Promise<void> {
    setOpen(false)
    await signOut()
    await navigate({ to: '/logga-in' })
  }

  return (
    <header className="topbar">
      <button
        ref={burgerRef}
        type="button"
        className="burger"
        aria-label="Meny"
        aria-haspopup="true"
        aria-expanded={open}
        aria-controls={panelId}
        onClick={() => setOpen((value) => !value)}
      >
        <span />
        <span />
        <span />
      </button>

      <span className="wordmark">Truppen</span>

      <div className={open ? 'drawer drawer--open' : 'drawer'} inert={!open}>
        <button
          type="button"
          className="drawer__overlay"
          aria-label="Stäng menyn"
          tabIndex={-1}
          onClick={close}
        />

        <nav ref={panelRef} id={panelId} className="drawer__panel" aria-label="Huvudmeny">
          <div className="drawer__head">
            <span className="wordmark">Truppen</span>
            <button
              type="button"
              className="drawer__close"
              aria-label="Stäng menyn"
              onClick={close}
            >
              <span aria-hidden="true">✕</span>
            </button>
          </div>

          <ul className="drawer__list">
            <li>
              <Link
                className="drawer__link"
                onClick={close}
                to="/"
                aria-current={current === 'matcher' ? 'page' : undefined}
              >
                Matcher
              </Link>
            </li>

            <li>
              <Link
                className="drawer__link"
                onClick={close}
                to="/spelarkort"
                aria-current={current === 'spelarkort' ? 'page' : undefined}
              >
                Spelarkort
              </Link>
            </li>

            {loggedIn && coachTeam !== null && (
              <li>
                <Link
                  className="drawer__link"
                  onClick={close}
                  to="/lag/$slug/tranare"
                  params={{ slug: coachTeam }}
                  aria-current={current === 'tranare' ? 'page' : undefined}
                >
                  Sköt laget
                </Link>
              </li>
            )}

            {loggedIn && (
              <li>
                <Link
                  className="drawer__link"
                  onClick={close}
                  to="/chatt"
                  aria-current={current === 'chatt' ? 'page' : undefined}
                >
                  Chatt
                </Link>
              </li>
            )}

            {loggedIn && (
              <li>
                <Link
                  className="drawer__link"
                  onClick={close}
                  to="/cuper"
                  aria-current={current === 'cuper' ? 'page' : undefined}
                >
                  Cuper
                </Link>
              </li>
            )}

            {loggedIn && adminOf.length > 0 && (
              <li>
                <Link
                  className="drawer__link"
                  onClick={close}
                  to="/admin"
                  aria-current={current === 'admin' ? 'page' : undefined}
                >
                  Tränare
                </Link>
              </li>
            )}

            {loggedIn && isSuperAdmin && (
              <li>
                <Link
                  className="drawer__link"
                  onClick={close}
                  to="/superadmin"
                  aria-current={current === 'superadmin' ? 'page' : undefined}
                >
                  Superadmin
                </Link>
              </li>
            )}

            {loggedIn && (
              <li>
                <Link
                  className="drawer__link"
                  onClick={close}
                  to="/konto"
                  aria-current={current === 'konto' ? 'page' : undefined}
                >
                  Mitt konto
                </Link>
              </li>
            )}

            {/*
              Ingenting alls medan status ar 'okand'. Sessionen fornyas vid start, och att visa
              "Logga in" under den halvsekunden hade blinkat till for varje inloggad foralder.
            */}
            {status === 'utloggad' && (
              <li>
                <Link
                  className="drawer__link"
                  onClick={close}
                  to="/logga-in"
                  aria-current={current === 'logga-in' ? 'page' : undefined}
                >
                  Logga in
                </Link>
              </li>
            )}

            {loggedIn && (
              <li>
                <button
                  type="button"
                  className="drawer__link drawer__link--action"
                  onClick={() => void handleSignOut()}
                >
                  Logga ut
                </button>
              </li>
            )}
          </ul>
        </nav>
      </div>
    </header>
  )
}

type Section =
  | 'matcher'
  | 'spelarkort'
  | 'chatt'
  | 'cuper'
  | 'tranare'
  | 'logga-in'
  | 'konto'
  | 'superadmin'
  | 'admin'
  | null

/**
 * Vilken meny­post adressen hör till.
 *
 * <para>
 * Matchdelen är tre adresser — startsidan, lagets schema och en enskild match — och alla
 * tre ska märka ut samma post. En adress som inte hör till någon post ger `null` i stället
 * för att falla tillbaka på den första: en 404-sida ska inte påstå att man står i schemat.
 * </para>
 */
function sectionOf(pathname: string): Section {
  if (pathname.endsWith('/tranare')) return 'tranare'
  if (pathname.startsWith('/spelarkort')) return 'spelarkort'
  if (pathname === '/logga-in') return 'logga-in'
  if (pathname === '/konto') return 'konto'
  if (pathname === '/superadmin') return 'superadmin'
  if (pathname === '/admin') return 'admin'
  if (pathname.startsWith('/chatt')) return 'chatt'
  if (pathname.startsWith('/cuper')) return 'cuper'

  if (pathname === '/' || pathname.startsWith('/lag/') || pathname.startsWith('/handelse/')) {
    return 'matcher'
  }

  return null
}
