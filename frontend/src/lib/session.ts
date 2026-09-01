import { readSetting, writeSetting } from '@/lib/storage'

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

/**
 * Att användaren *har* loggat in någon gång på den här telefonen.
 *
 * <h3>Varför det här får ligga i lagringen</h3>
 *
 * Det är ingen behörighet. Flaggan avgör bara om appen ska *försöka* förnya sessionen vid
 * start — den som sätter den för hand blir inte inloggad, bara mötd av ett 401.
 *
 * Utan flaggan skulle varje besök göra ett anrop mot inloggningen, även för de allra
 * flesta som aldrig loggar in. Det anropet kan inte cachas på Vercels edge och skulle
 * alltså väcka Render varje gång någon öppnar schemat — de femtio sekunderna §KM.11 finns
 * till för att undvika.
 */
const SESSION_HINT_KEY = 'karra.har-loggat-in'

export function getAccessToken(): string | null {
  return accessToken
}

export function setAccessToken(token: string | null): void {
  accessToken = token

  if (token !== null) {
    writeSetting(SESSION_HINT_KEY, '1')
  }

  sessionChanged?.()
}

/** Rensar allt appen håller om sessionen. Cookien rensas av servern. */
export function clearSession(): void {
  accessToken = null
  writeSetting(SESSION_HINT_KEY, '')
  sessionChanged?.()
}

/** Sant om det är värt att försöka förnya sessionen vid start. */
export function hasSessionHint(): boolean {
  return readSetting(SESSION_HINT_KEY) === '1'
}
