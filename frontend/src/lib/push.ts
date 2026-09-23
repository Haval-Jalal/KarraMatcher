import { getAuthJson, postJson } from '@/lib/api'
import { readSetting, writeSetting } from '@/lib/storage'

/**
 * Klientens del av webbnotiser (`#244`, §KM.10).
 *
 * <h3>Var gränsen går</h3>
 *
 * Backend skickar redan notiser, men ingen enhet prenumererar. Den här modulen gör det:
 * frågar om tillstånd, prenumererar enheten via `pushManager`, och lämnar prenumerationen
 * till servern per lag. Själva visningen sker i service workern (`push`/`notificationclick`).
 *
 * <h3>Adressen är känslig</h3>
 *
 * En push-adress pekar ut en enskild enhet lika bra som ett telefonnummer (§KM.10). Den
 * loggas aldrig här, och servern svarar aldrig med den. Vi behåller bara ett litet på/av
 * per lag i enhetens egen lagring, så att växeln visar rätt läge — aldrig adressen.
 */

/** Svar från VAPID-nyckelendpointen. 404 när push inte är konfigurerat på servern. */
interface PushKeyResponse {
  publicKey: string
}

/** Vad ett försök att slå på notiser landade i. */
export type EnablePushResult = 'enabled' | 'denied' | 'unsupported' | 'error'

const enabledKey = (teamSlug: string) => `karra.push.${teamSlug}`

/** Sant om webbläsaren över huvud taget kan ta emot webbnotiser. */
export function isPushSupported(): boolean {
  return 'serviceWorker' in navigator && 'PushManager' in globalThis && 'Notification' in globalThis
}

/** Tillståndet just nu: aldrig frågat, tillåtet, eller nekat. */
export function notificationPermission(): NotificationPermission {
  return isPushSupported() ? Notification.permission : 'denied'
}

/**
 * Om den här enheten har notiser på för laget.
 *
 * <para>
 * Läses synkront ur enhetens lagring plus tillståndet, så att växeln kan visa rätt läge utan
 * ett laddningstillstånd. Servern har ingen läs-endpoint för det — prenumerationen är
 * enhetens, och det är här den hör hemma.
 * </para>
 */
export function isTeamPushEnabled(teamSlug: string): boolean {
  return notificationPermission() === 'granted' && readSetting(enabledKey(teamSlug)) === '1'
}

/**
 * Slår på notiser för laget på den här enheten.
 *
 * Frågar om tillstånd om det inte redan getts, prenumererar enheten mot VAPID-nyckeln från
 * servern, och lämnar prenumerationen till laget. Svarar med vad som hände så att
 * gränssnittet kan säga rätt sak — ett nekat tillstånd är inte ett fel, bara ett nej.
 */
export async function enableTeamPush(teamSlug: string): Promise<EnablePushResult> {
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
      `/api/v1/teams/${encodeURIComponent(teamSlug)}/push`,
      { endpoint, p256dh: keys.p256dh, auth: keys.auth },
      { method: 'POST' },
    )

    writeSetting(enabledKey(teamSlug), '1')

    return 'enabled'
  } catch {
    // Nätet, en avvisad prenumeration, eller push avstängt på servern (404 på nyckeln).
    return 'error'
  }
}

/**
 * Slår av notiser för laget på den här enheten.
 *
 * <para>
 * Tar bort lagets koppling på servern, men avregistrerar <b>inte</b> webbläsarens
 * prenumeration — samma enhet kan ha notiser på för ett annat lag, och den prenumerationen
 * delas. Svarar med om avregistreringen mot servern gick; en miss får inte låta växeln se
 * avstängd ut medan servern fortfarande skickar.
 * </para>
 */
export async function disableTeamPush(teamSlug: string): Promise<boolean> {
  try {
    let endpoint: string | undefined

    if (isPushSupported()) {
      const registration = await navigator.serviceWorker.ready
      const subscription = await registration.pushManager.getSubscription()
      endpoint = subscription?.endpoint
    }

    // Utan känd adress finns inget att avregistrera mot servern; lagringen städas ändå.
    if (endpoint !== undefined) {
      await postJson<void>(
        `/api/v1/teams/${encodeURIComponent(teamSlug)}/push`,
        { endpoint },
        { method: 'DELETE' },
      )
    }

    writeSetting(enabledKey(teamSlug), '')

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
