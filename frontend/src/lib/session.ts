/**
 * Sessionen så som klienten håller den.
 *
 * <h3>Access-token bor i minnet</h3>
 *
 * Aldrig i `localStorage` eller `sessionStorage`. En token i webbläsarens lagring går att
 * läsa för vilket skript som helst på sidan, och överlever dessutom fliken — den blir
 * kvar på en lånad telefon långt efter att någon slutat använda appen. I en modulvariabel
 * försvinner den när fliken stängs, vilket är precis vad man vill.
 *
 * Refresh-token finns inte här alls. Den ligger i en `httpOnly`-cookie som JavaScript inte
 * kommer åt, och skickas av webbläsaren själv (§KM.11).
 *
 * <h3>Ingen ledtråd i lagringen (v2, `#255`)</h3>
 *
 * Tidigare fanns en `localStorage`-flagga som avgjorde om appen ens skulle *försöka*
 * förnya sessionen vid start. Den togs bort: iOS gallrar skrivbar lagring (localStorage)
 * efter ~7 dagar, medan den `httpOnly` refresh-cookien lever 60 dagar och är undantagen —
 * så en installerad app kunde sitta på en giltig cookie men vägra använda den. I stängda
 * v2 finns inga anonyma besökare, så det som flaggan skyddade mot (att väcka Render för en
 * anonym schema-läsare, §KM.11) finns inte längre. Appen försöker i stället alltid förnya
 * mot cookien vid kallstart; en gäst får ett ofarligt `401`.
 */

let accessToken: string | null = null

/**
 * Anropas när inloggningen ändras — vid inloggning, förnyelse och utloggning.
 *
 * <h3>Varför det här behövs</h3>
 *
 * ASP.NET binder anti-forgery-token till användarens identitet. En token som hämtats
 * utloggad gäller **inte** för ett inloggat anrop. Utan den här signalen hade appen
 * fortsatt använda den gamla token efter en inloggning, och varje anrop som ändrar något
 * hade svarat 400 — inklusive kontoraderingen.
 *
 * Bindningen är avsiktlig och bra: den gör en stulen CSRF-token oanvändbar för någon
 * annans session. Det är klienten som måste hänga med.
 */
let sessionChanged: (() => void) | null = null

export function onSessionChange(listener: () => void): void {
  sessionChanged = listener
}

export function getAccessToken(): string | null {
  return accessToken
}

export function setAccessToken(token: string | null): void {
  accessToken = token
  sessionChanged?.()
}

/** Rensar allt appen håller om sessionen. Cookien rensas av servern. */
export function clearSession(): void {
  accessToken = null
  sessionChanged?.()
}
