import { Link } from '@tanstack/react-router'

import { useDocumentTitle } from '@/lib/useDocumentTitle'

/**
 * "Om appen" — vad Truppen är, och hur den hanterar barnens uppgifter.
 *
 * <h3>Varför sidan finns</h3>
 *
 * En förälder ska kunna avgöra om appen är att lita på innan hen lägger in sitt lag. Det som är
 * värt att säga rakt ut är att appen är <em>byggd för att samla så lite som möjligt</em> om ett
 * barn — det är förtroendeargumentet, och det står här som översikt.
 *
 * <h3>Ärlig, inte säljande</h3>
 *
 * Tonen är densamma som i resten av appen: inga absoluta löften ("omöjligt att läcka"), utan det
 * konkreta — vad som inte ens finns hos oss kan inte läcka härifrån. Detaljerna om vad som sparas
 * bor kvar i <Link to="/integritet">Så hanteras dina uppgifter</Link>; den här sidan är översikten.
 */
export function AboutPage() {
  useDocumentTitle('Om appen')

  return (
    <main className="prose">
      <header className="app-header">
        <h1>Om Truppen</h1>
        <p className="app-header__subtitle">
          En enkel app för lagets föräldrar och tränare — schema, kallelser, samåkning, chatt och
          ett spelarkort för barnet.
        </p>
      </header>

      <p>
        Truppen finns för att göra lördagens matchtid, vem som kör och vilka som kommer lika lätt
        att hålla reda på i telefonen som det en gång var på en papperslapp — fast utan lappen.
      </p>

      <section aria-labelledby="integritet">
        <h2 id="integritet">Byggd för att veta så lite som möjligt</h2>
        <p>
          Om ett barn sparar servern bara <strong>förnamn, efternamnets första bokstav</strong> och
          vilket lag barnet hör till — aldrig hela efternamnet, ingen adress, inget personnummer,
          inget födelsedatum, inget foto. Ett barn visas som "Liam J", inte mer.
        </p>
        <p>
          Barnets <strong>spelarkort</strong> — mål, matcher, märken — lämnar aldrig telefonen. Det
          skickas aldrig till någon server, så vi kan inte se det och det kan inte läcka från oss.
          Det är familjens egen sak.
        </p>
        <p>
          Appen har <strong>ingen spårning</strong> och inga tredjepartsskript som följer dig. Logik
          som denna — att det som aldrig samlas in inte kan komma på avvägar — är hela idén bakom
          hur appen är byggd.
        </p>
        <p>
          Vill du veta exakt vad som sparas, var, hur länge och hur du raderar det, står allt i{' '}
          <Link to="/integritet">Så hanteras dina uppgifter</Link>.
        </p>
      </section>

      <section aria-labelledby="stangd">
        <h2 id="stangd">Bara för laget</h2>
        <p>
          Truppen är en sluten krets: innehållet — scheman, kallelser, samåkning, chatt — syns bara
          för inloggade medlemmar i laget. En delad länk leder till en inloggning, inte in i någon
          annans lag.
        </p>
      </section>
    </main>
  )
}
