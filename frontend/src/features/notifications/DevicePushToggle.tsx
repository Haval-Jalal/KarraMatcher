import { useState } from 'react'

import {
  disablePush,
  enablePush,
  isPushEnabled,
  isPushSupported,
  notificationPermission,
} from '@/lib/push'

import { NotificationInstallTip } from './NotificationInstallTip'

/**
 * På/av för webbnotiser på den här enheten (`#244`, per enhet sedan `#332`-uppföljningen).
 *
 * <h3>Per enhet, en enda växel</h3>
 *
 * En prenumeration hör till webbläsaren på just den här telefonen — samma förälder som loggar
 * in på en annan enhet slår på notiser där för sig. Det är <b>en</b> växel per enhet (inte en
 * per lag): utskicket väljer mottagare på medlemskap, så enheten får en notis, inte flera.
 * Den globala på/av-växeln ovanför styr om kontot vill ha notiser alls.
 *
 * <h3>Ärlig om tillståndet</h3>
 *
 * Webbläsaren kan sakna stöd, ha blockerat notiser, eller — på iOS — kräva att appen ligger
 * på hemskärmen. Var och en av de sakerna får ett eget, begripligt besked i stället för en
 * växel som inte gör något.
 */
export function DevicePushToggle() {
  const [enabled, setEnabled] = useState(() => isPushEnabled())
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  if (!isPushSupported()) {
    return (
      <div className="notif-device">
        <p className="notif-settings__tip" role="note">
          Den här webbläsaren kan inte visa notiser. Schemat fungerar ändå.
        </p>
        <NotificationInstallTip />
      </div>
    )
  }

  const denied = notificationPermission() === 'denied'

  async function toggle(): Promise<void> {
    setBusy(true)
    setMessage(null)

    if (enabled) {
      const stopped = await disablePush()

      if (stopped) {
        setEnabled(false)
      } else {
        setMessage('Det gick inte att stänga av just nu. Försök igen om en stund.')
      }
    } else {
      const result = await enablePush()

      if (result === 'enabled') {
        setEnabled(true)
      } else if (result === 'denied') {
        setMessage(
          'Notiser är blockerade. Tillåt dem för den här sidan i webbläsarens inställningar.',
        )
      } else {
        setMessage('Det gick inte att slå på notiser just nu. Försök igen om en stund.')
      }
    }

    setBusy(false)
  }

  return (
    <div className="notif-device">
      <label className="notif-settings__row">
        <input
          type="checkbox"
          checked={enabled}
          disabled={busy || denied}
          onChange={() => {
            void toggle()
          }}
        />
        <span>
          <strong>Notiser på den här enheten</strong>
          <br />
          Få notiser direkt hit. Varje telefon slås på för sig.
        </span>
      </label>

      {denied && (
        <p className="notif-settings__tip" role="note">
          Notiser är blockerade i webbläsaren. Tillåt dem för den här sidan i webbläsarens
          inställningar, så kan du slå på dem här.
        </p>
      )}

      {message !== null && (
        <p className="state state--error" role="alert">
          {message}
        </p>
      )}

      <NotificationInstallTip />
    </div>
  )
}
