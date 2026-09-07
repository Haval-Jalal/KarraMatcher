import { Outlet, useRouterState } from '@tanstack/react-router'
import { useEffect, useRef } from 'react'

import { AppNav } from '@/components/AppNav'

/**
 * Ramen runt varje sida.
 *
 * <h3>Hoppa till innehållet</h3>
 *
 * Lagväljaren upprepas på varje lagsida. Utan en hopplänk måste den som navigerar med
 * tangentbord tabba förbi fyra länkar varje gång hen byter sida (WCAG 2.4.1). Länken syns
 * först när den får fokus.
 *
 * <h3>Fokus vid sidbyte</h3>
 *
 * I en ensidesapp byter innehållet utan att fokus flyttas, så en skärmläsare fortsätter
 * läsa där den stod — ofta mitt i den gamla sidan. Fokus flyttas därför till den nya
 * sidans början vid varje adressbyte, men inte vid första inläsningen, då webbläsaren
 * redan gör rätt.
 */
export function RootLayout() {
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const target = useRef<HTMLDivElement>(null)
  const first = useRef(true)

  useEffect(() => {
    if (first.current) {
      first.current = false
      return
    }

    target.current?.focus()
  }, [pathname])

  return (
    <>
      <a className="skip-link" href="#innehall">
        Hoppa till innehållet
      </a>

      {/*
        Menyn star fore innehallet, alltsa efter hopplanken. Den som anvander tangentbord
        kan da hoppa forbi den; den som vill navigera tabbar in i den direkt.
      */}
      <AppNav />

      {/*
        tabIndex={-1} gör elementet fokuserbart från kod utan att lägga det i tabbordningen.
        Utan det går fokus inte att flytta hit, och hopplänken tar en dit utan att
        skärmläsaren följer med.
      */}
      <div id="innehall" ref={target} tabIndex={-1}>
        <Outlet />
      </div>
    </>
  )
}
