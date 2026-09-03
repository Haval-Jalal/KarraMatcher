import { useState } from 'react'

import { isInstalled, isIos } from '@/lib/platform'
import { readSetting, writeSetting } from '@/lib/storage'

/**
 * Tipset om att lägga appen på hemskärmen. Bara på iOS, bara en gång.
 *
 * <h3>Varför det här inte är en finess</h3>
 *
 * iOS rensar lagring för webbplatser som inte besökts på sju dagar. Appar på hemskärmen är
 * undantagna. Spelarkortet finns bara på telefonen (§KM.2) och appen används säsongsvis —
 * en förälder med bara ett bokmärke kan alltså förlora en hel säsong under sommaruppehållet
 * utan att ha gjort något fel.
 *
 * <h3>Det tjatar inte</h3>
 *
 * Tre villkor måste gälla samtidigt: det är iOS, appen ligger inte redan på hemskärmen, och
 * tipset har inte stängts tidigare. Ett bortklickat tips kommer inte tillbaka.
 *
 * Det visas dessutom först när kortet har innehåll. Samma resonemang som bakom
 * <c>persist()</c>-frågan: den som öppnat appen för att se en matchtid har ingenting att
 * förlora ännu, och ett råd om något man inte gör är precis det som lär folk att sluta läsa
 * appens texter.
 */

const DISMISSED_KEY = 'karra.hemskarmstips'

export function InstallTip({ hasContent }: { hasContent: boolean }) {
  const [dismissed, setDismissed] = useState(() => readSetting(DISMISSED_KEY) === 'ja')

  if (dismissed || !hasContent || !isIos() || isInstalled()) {
    return null
  }

  return (
    <section className="tip">
      {/*
        h2 och inte h3: rutan ar ett eget avsnitt pa sidan, och sidans forsta rubrik under
        h1 far inte hoppa over en niva (WCAG 1.3.1).
      */}
      <h2>Lägg appen på hemskärmen</h2>

      <p>
        {/*
          Sambandet ar hela poangen med rutan. Utan det ar det ett tips om en genvag; med
          det ar det skalet att gora det innan sommaruppehallet.
        */}
        På iPhone och iPad rensas sparad data för webbsidor som inte använts på en vecka.{' '}
        <strong>Appar på hemskärmen slipper det</strong> — så statistiken ligger kvar mellan
        säsongerna.
      </p>

      <p className="tip__how">
        Tryck på <b>Dela</b> längst ner, välj <b>Lägg till på hemskärmen</b>.
      </p>

      <button
        type="button"
        className="button"
        onClick={() => {
          writeSetting(DISMISSED_KEY, 'ja')
          setDismissed(true)
        }}
      >
        Tack, jag vet
      </button>
    </section>
  )
}
