/**
 * Var spelarkortet finns, sagt rakt ut.
 *
 * <h3>Varför texten står framme och inte i en engångsruta</h3>
 *
 * Kortet lämnar aldrig telefonen (§KM.2). Det är appens starkaste integritetsskydd och
 * samtidigt dess enda verkliga risk: byts telefonen utan säkerhetskopia är säsongen borta.
 *
 * En ruta man klickar bort en gång läses av den som ännu inte har något att förlora. Den
 * som har en halv säsong ifylld ska kunna gå tillbaka och läsa vad som gäller — därför står
 * texten kvar, alltid, på samma sida som datan.
 *
 * <h3>Tonen</h3>
 *
 * Samma ärlighet som föregångarens <em>"Sparas bara på den här telefonen"</em>: ett
 * konstaterande, inte en brasklapp och inte en varning. Föräldern ska kunna fatta ett
 * informerat beslut om att säkerhetskopiera — inte bli skrämd till det.
 */
export function StorageNotice() {
  return (
    <section className="notice">
      <p className="notice__lead">
        <strong>Allt här sparas bara på den här telefonen.</strong> Du behöver inget konto, och
        ingen annan kan se det — men statistiken följer med telefonen, inte med dig.
      </p>

      <details className="notice__more">
        <summary>Vad betyder det?</summary>

        <p>
          Barnen, målen och matchrapporterna ligger i den här webbläsaren. De skickas aldrig till
          någon server, så vi kan inte läsa dem, inte tappa bort dem — och inte hämta tillbaka dem
          åt dig.
        </p>

        <p>Det försvinner om du:</p>

        <ul>
          <li>byter telefon</li>
          <li>rensar webbläsarens data</li>
          <li>använder appen i ett privat fönster</li>
        </ul>

        <p>
          <strong>Säkerhetskopian längre ner är det som skyddar dig.</strong> Kopiera koden och
          spara den någonstans du hittar den igen, så kan du läsa in säsongen på en ny telefon.
        </p>

        <p>
          Har ni två telefoner i familjen får var och en sin egen statistik. Det följer av att inget
          sparas centralt, och går att lösa genom att dela koden med varandra.
        </p>

        {/*
          Radet star har permanent, och inte bara i den bortklickbara rutan. Den som stangt
          tipset i varas ska kunna hitta skalet igen till hosten -- och det ar just over
          sommaruppehallet som iOS hinner rensa.
        */}
        <p>
          <strong>Hemskärmen skyddar datan.</strong> På iPhone och iPad rensas sparad data för
          webbsidor som inte använts på en vecka — appar på hemskärmen slipper det, så statistiken
          ligger kvar mellan säsongerna. Tryck på <b>Dela</b> längst ner och välj{' '}
          <b>Lägg till på hemskärmen</b>.
        </p>
      </details>
    </section>
  )
}
