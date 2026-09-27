import { getAuthJson, postJson } from '@/lib/api'
import { readSetting, writeSetting } from '@/lib/storage'

/**
 * Klientens del av webbnotiser (`#244`, §KM.10).
 *
 * <h3>Var gränsen går</h3>
 *
 * Backend skickar redan notiser, men ingen enhet prenumererar. Den här modulen gör det:
 * frågar om tillstånd, prenumererar enheten via `pushManager`, och lämnar prenumerationen
 * till servern. Själva visningen sker i service workern (`push`/`notificationclick`).
 *
 * <h3>Per enhet, inte per lag (`#332`-uppföljning)</h3>
 *
 * En prenumeration hör till <b>webbläsaren</b> — en enda på/av per enhet, inte en per lag.
 * Utskicket väljer mottagare på medlemskap, så en enhet får en notis. Förr fanns en rad per
 * lag med samma adress, vilket gav samma enhet flera identiska notiser.
 *
 * <h3>Adressen är känslig</h3>
 *
 * En push-adress pekar ut en enskild enhet lika bra som ett telefonnummer (§KM.10). Den
 * loggas aldrig här, och servern svarar aldrig med den. Vi behåller bara ett litet på/av
 * i enhetens egen lagring, så att växeln visar rätt läge — aldrig adressen.
 */

/** Svar från VAPID-nyckelendpointen. 404 när push inte är konfigurerat på servern. */
interface PushKeyResponse {
  publicKey: string
}

/** Vad ett försök att slå på notiser landade i. */
export type EnablePushResult = 'enabled' | 'denied' | 'unsupported' | 'error'

const ENABLED_KEY = 'karra.push'

/** Sant om webbläsaren över huvud taget kan ta emot webbnotiser. */
export function isPushSupported(): boolean {
  return 'serviceWorker' in navigator && 'PushManager' in globalThis && 'Notification' in globalThis
}

/** Tillståndet just nu: aldrig frågat, tillåtet, eller nekat. */
export function notificationPermission(): NotificationPermission {
  return isPushSupported() ? Notification.permission : 'denied'
}

/**
 * Om den här enheten har notiser på.
 *
 * <para>
 * Läses synkront ur enhetens lagring plus tillståndet, så att växeln kan visa rätt läge utan
 * ett laddningstillstånd. Servern har ingen läs-endpoint för det — prenumerationen är
 * enhetens, och det är här den hör hemma.
 * </para>
 */
export function isPushEnabled(): boolean {
  return notificationPermission() === 'granted' && readSetting(ENABLED_KEY) === '1'
}

/**
 * Slår på notiser på den här enheten (`#332`-uppföljning).
 *
 * Frågar om tillstånd om det inte redan getts, prenumererar enheten mot VAPID-nyckeln från
 * servern, och lämnar prenumerationen till kontot. Svarar med vad som hände så att
 * gränssnittet kan säga rätt sak — ett nekat tillstånd är inte ett fel, bara ett nej.
 */
export async function enablePush(): Promise<EnablePushResult> {
  if (!isPushSupported()) {
    return 'unsupported'
  }

  const permission =
    Notification.permission === 'default'
      ? await Notification.requestPermission()
      : Notification.permission

  if (permission !== 'granted') {
    return 'denied'
  }

  try {
    const { publicKey } = await getAuthJson<PushKeyResponse>('/api/v1/push/key')

    const registration = await navigator.serviceWorker.ready

    const subscription =
      (await registration.pushManager.getSubscription()) ??
      (await registration.pushManager.subscribe({
        // Krav i Chrome: en prenumeration måste leda till en synlig notis. Det är precis
        // vad vi gör — tyst push finns inte här.
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicKey),
      }))

    const { endpoint, keys } = subscription.toJSON()

    if (endpoint === undefined || keys?.p256dh === undefined || keys.auth === undefined) {
      return 'error'
    }

    await postJson<void>(
      '/api/v1/push',
      { endpoint, p256dh: keys.p256dh, auth: keys.auth },
      { method: 'POST' },
    )

    writeSetting(ENABLED_KEY, '1')

    return 'enabled'
  } catch {
    // Nätet, en avvisad prenumeration, eller push avstängt på servern (404 på nyckeln).
    return 'error'
  }
}

/**
 * Slår av notiser på den här enheten (`#332`-uppföljning).
 *
 * <para>
 * Tar bort prenumerationen på servern och avregistrerar webbläsarens push-prenumeration — en
 * enhet, en prenumeration. Svarar med om det gick; en miss får inte låta växeln se avstängd ut
 * medan servern fortfarande skickar.
 * </para>
 */
export async function disablePush(): Promise<boolean> {
  try {
    let endpoint: string | undefined

    if (isPushSupported()) {
      const registration = await navigator.serviceWorker.ready
      const subscription = await registration.pushManager.getSubscription()
      endpoint = subscription?.endpoint

      // En enhet, en prenumeration: avregistrera webbläsaren också, inte bara serverraden.
      await subscription?.unsubscribe()
    }

    // Utan känd adress finns inget att avregistrera mot servern; lagringen städas ändå.
    if (endpoint !== undefined) {
      await postJson<void>('/api/v1/push', { endpoint }, { method: 'DELETE' })
    }

    writeSetting(ENABLED_KEY, '')

    return true
  } catch {
    return false
  }
}

/**
 * Översätter VAPID-nyckelns base64url till de byte `applicationServerKey` vill ha.
 *
 * <para>
 * Nyckeln kommer base64url-kodad (`-`/`_` i stället för `+`/`/`, utan utfyllnad). `atob`
 * förstår bara vanlig base64, så vi byter tillbaka tecknen och fyller på — annars avvisas
 * prenumerationen med ett svårtytt fel.
 * </para>
 */
function urlBase64ToUint8Array(base64Url: string): Uint8Array<ArrayBuffer> {
  const padding = '='.repeat((4 - (base64Url.length % 4)) % 4)
  const base64 = (base64Url + padding).replace(/-/g, '+').replace(/_/g, '/')
  const raw = atob(base64)

  // Uttrycklig ArrayBuffer i botten: `applicationServerKey` vill ha en BufferSource över en
  // ArrayBuffer, inte en delad buffer, och typerna skiljer på det.
  const bytes = new Uint8Array(new ArrayBuffer(raw.length))
  for (let i = 0; i < raw.length; i += 1) {
    bytes[i] = raw.charCodeAt(i)
  }

  return bytes
}
