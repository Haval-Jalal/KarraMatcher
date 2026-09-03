/**
 * Vad för slags enhet appen körs på — och om den redan är installerad.
 *
 * <h3>Varför appen behöver veta det</h3>
 *
 * iOS rensar lagring för webbplatser som inte besökts på sju dagar. Appar som lagts på
 * hemskärmen är undantagna. Spelarkortet finns bara på telefonen (§KM.2), och den här
 * appen används säsongsvis med långa uppehåll — en förälder med bara ett bokmärke kan
 * alltså förlora en hel säsong under sommaren utan att ha gjort något fel.
 *
 * Installationstipset måste därför veta två saker: att det <em>är</em> iOS, och att appen
 * inte redan ligger på hemskärmen. Att tjata på någon som redan gjort det är det snabbaste
 * sättet att lära folk att ignorera appens texter.
 */

/**
 * Sant på iPhone och iPad.
 *
 * <para>
 * iPadOS 13 och senare uppger sig vara en Mac, så den känns igen på att den har
 * pekskärm — en riktig Mac har inte det. Det är den etablerade omvägen, och den behövs:
 * en iPad är en helt vanlig enhet att läsa matchschemat på.
 * </para>
 *
 * <para>
 * User agent-sniffning är annars något man ska undvika. Här går det inte att ersätta med
 * en förmågekontroll, eftersom det som skiljer inte är en förmåga utan en
 * <b>lagringspolicy</b> — och den syns inte i något API.
 * </para>
 */
export function isIos(): boolean {
  const agent = globalThis.navigator?.userAgent ?? ''

  if (/iPhone|iPad|iPod/.test(agent)) {
    return true
  }

  return /Macintosh/.test(agent) && (globalThis.navigator?.maxTouchPoints ?? 0) > 1
}

/**
 * Sant när appen körs från hemskärmen i stället för i en webbläsarflik.
 *
 * <para>
 * Två kontroller, för att de täcker olika webbläsare: `display-mode: standalone` är
 * standarden, medan iOS Safari länge bara haft `navigator.standalone`.
 * </para>
 */
export function isInstalled(): boolean {
  const legacy = (globalThis.navigator as { standalone?: boolean } | undefined)?.standalone

  if (legacy === true) {
    return true
  }

  try {
    return globalThis.matchMedia?.('(display-mode: standalone)').matches === true
  } catch {
    // matchMedia saknas i vissa testmiljoer och kan kasta pa en ogiltig fraga.
    return false
  }
}
