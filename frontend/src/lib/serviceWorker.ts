/**
 * Registrerar service workern och upptäcker nya versioner.
 *
 * <h3>Varför användaren tillfrågas</h3>
 *
 * En ny version tar inte över av sig själv. Att byta kod under fötterna på någon som läser
 * en matchsida är inte hjälpsamt — och en halvt utbytt app är svårare att förstå än en
 * gammal. Den nya versionen väntar tills användaren säger till (Säkerhetschecklistan 5.5).
 *
 * <h3>Bara över HTTPS</h3>
 *
 * Webbläsaren tillåter ändå inte annat än HTTPS eller localhost, men vi registrerar inte
 * ens i utveckling: en service worker som cachar under `npm run dev` gör att man felsöker
 * gammal kod utan att förstå varför (checklistan 5.4).
 */

export interface ServiceWorkerHooks {
  /** Anropas när en ny version står redo. Argumentet aktiverar den och laddar om. */
  onUpdateReady: (applyUpdate: () => void) => void
}

export function registerServiceWorker({ onUpdateReady }: ServiceWorkerHooks): void {
  if (!('serviceWorker' in navigator) || !import.meta.env.PROD) {
    return
  }

  navigator.serviceWorker
    .register('/sw.js', { scope: '/' })
    .then((registration) => {
      // Sidan laddades med en gammal version och en ny väntar redan.
      if (registration.waiting) {
        offer(registration.waiting, onUpdateReady)
      }

      registration.addEventListener('updatefound', () => {
        const installing = registration.installing

        if (!installing) {
          return
        }

        installing.addEventListener('statechange', () => {
          // `controller` saknas vid allra första installationen. Då finns ingen gammal
          // version att ersätta, och att fråga om omladdning vore obegripligt.
          if (installing.state === 'installed' && navigator.serviceWorker.controller) {
            offer(installing, onUpdateReady)
          }
        })
      })
    })
    .catch(() => {
      // En misslyckad registrering ska aldrig fälla appen. Utan service worker fungerar
      // allt som vanligt, bara inte offline.
    })

  let reloading = false

  navigator.serviceWorker.addEventListener('controllerchange', () => {
    // Skyddar mot en omladdningsloop om flera flikar byter samtidigt.
    if (reloading) {
      return
    }

    reloading = true
    globalThis.location.reload()
  })
}

function offer(worker: ServiceWorker, onUpdateReady: ServiceWorkerHooks['onUpdateReady']) {
  onUpdateReady(() => {
    worker.postMessage('SKIP_WAITING')
  })
}
