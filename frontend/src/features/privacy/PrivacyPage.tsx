import { Link } from '@tanstack/react-router'

import { useDocumentTitle } from '@/lib/useDocumentTitle'

/**
 * Integritetstexten (`#66`, §KM.6).
 *
 * <h3>Skriven för en förälder, inte en jurist</h3>
 *
 * Den ska gå att läsa på en telefon i en bilkö och faktiskt förstås. Därför korta stycken,
 * vanliga ord, och det viktigaste först: vad som stannar i telefonen, vad kontot sparar, och
 * den enda uppgift som lämnar EU. Ingenting gömt bakom "vi kan komma att dela med
 * underleverantörer".
 */
export function PrivacyPage() {
  useDocumentTitle('Så hanteras dina uppgifter')

  return (
    <main className="prose">
      <header className="app-header">
        <h1>Så hanteras dina uppgifter</h1>
        <p className="app-header__subtitle">
          Kort om vad appen sparar, var, hur länge, och hur du raderar det.
        </p>
      </header>

      <p>
        Appen är byggd för att samla så lite som möjligt om er. Lagets innehåll kräver inloggning;
        spelarkortet på din telefon når du utan konto och det lämnar aldrig telefonen. Nedan står
        precis vad som sparas.
      </p>

      <section aria-labelledby="spelarkortet">
        <h2 id="spelarkortet">Spelarkortet stannar i telefonen</h2>
        <p>
          Barnets matchresultat, mål, assist, spelade matcher och märken sparas{' '}
          <strong>bara på den här telefonen</strong>. De skickas aldrig till våra servrar — vi kan
          inte se dem, och de kan inte läcka från oss.
        </p>
        <p>
          Baksidan: byter du telefon, rensar webbläsardata eller raderar appen utan att först spara{' '}
          <strong>säkerhetskopieringskoden</strong> är statistiken borta. Två telefoner i familjen
          har varsin uppsättning. Spelarkortet kräver inget konto.
        </p>
      </section>

      <section aria-labelledby="kontot">
        <h2 id="kontot">Kontot</h2>
        <p>
          Loggar du in sparar vi din <strong>mejladress</strong>, och ditt namn om du väljer att
          fylla i det. Inget mer — inga uppgifter om barn, inget telefonnummer. Namnet visas bara
          för inloggade i laget, i samåkningen.
        </p>
      </section>

      <section aria-labelledby="samakning">
        <h2 id="samakning">Samåkning</h2>
        <p>
          Lägger du upp en skjuts eller frågar om en plats sparar vi det du fyllt i: riktning,
          avgångsplats, tid, antal platser och de egna ord du skriver i notisen eller hälsningen.
          Appen frågar aldrig efter ditt telefonnummer.
        </p>
        <p>
          En matchs hela samåkning <strong>raderas 30 dagar efter matchen</strong>. Det du skriver
          visas bara för de inblandade och lagets tränare.
        </p>
      </section>

      <section aria-labelledby="notiser">
        <h2 id="notiser">Notiser</h2>
        <p>
          Tillåter du notiser sparar vi en teknisk adress till din webbläsare, så att beskedet kan
          skickas dit. Du väljer själv vilka notiser du vill ha per lag, och kan stänga av dem när
          som helst.
        </p>
      </section>

      <section aria-labelledby="ingen-sparning">
        <h2 id="ingen-sparning">Ingen spårning</h2>
        <p>
          Ingen besöksanalys, inga annonser, inga spårningsskript. De enda utomstående tjänster
          appen använder är väderprognosen (Open-Meteo) och kartlänkar när du ber om vägbeskrivning.
        </p>
      </section>

      <section aria-labelledby="eu">
        <h2 id="eu">En uppgift lämnar EU</h2>
        <p>
          När du loggar in skickas en engångskod till din mejladress. Utskicket sker via tjänsten{' '}
          <strong>Resend</strong>, som lagrar sändningsloggar i <strong>USA</strong>. Din mejladress
          lämnar alltså EU i det ögonblicket.
        </p>
        <p>
          Det är den <strong>enda</strong> uppgift som lämnar EU, och det är en vuxens mejladress —
          aldrig något om ett barn. Överföringen vilar på <strong>Data Privacy Framework</strong>,
          den överenskommelse som tillåter överföring av personuppgifter mellan EU och godkända
          företag i USA.
        </p>
      </section>

      <section aria-labelledby="hur-lange">
        <h2 id="hur-lange">Så länge sparas det</h2>
        <ul>
          <li>
            <strong>Spelarkortet:</strong> bara i telefonen, tills du raderar det själv.
          </li>
          <li>
            <strong>Kontot:</strong> tills du raderar det.
          </li>
          <li>
            <strong>Samåkning:</strong> 30 dagar efter matchen.
          </li>
          <li>
            <strong>Inloggningskoden:</strong> tio minuter, sedan är den värdelös.
          </li>
        </ul>
      </section>

      <section aria-labelledby="radera">
        <h2 id="radera">Så raderar du</h2>
        <p>
          Spelarkortet raderar du på spelarkortssidan — det ligger bara i telefonen. Kontot och allt
          som hör till det (samåkning, notisinställningar) raderar du på{' '}
          <Link to="/konto">Mitt konto</Link>. Det sker direkt och går inte att ångra.
        </p>
      </section>
    </main>
  )
}
