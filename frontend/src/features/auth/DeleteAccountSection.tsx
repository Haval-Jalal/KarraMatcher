import { useState } from 'react'

import { ApiError } from '@/lib/api'

import { deleteAccount } from './authApi'

/**
 * Radera kontot.
 *
 * <h3>Bekräftelsen går inte att klicka igenom av misstag</h3>
 *
 * Två steg, och det andra steget har en knapp som inte står där det första hade en. En
 * dubbelklickning på samma ställe ska inte kunna radera ett konto — det finns ingen
 * ångerknapp, och §KM.6 kräver att raderingen sker på riktigt.
 *
 * <h3>Spelarkortet</h3>
 *
 * Det berörs inte, och kan inte beröras: det har aldrig nått servern (§KM.2). Att säga
 * det rakt ut är hela poängen — en förälder som raderar sitt konto ska inte tro att
 * barnets statistik försvann med det, och inte heller bli förvånad över att den finns
 * kvar.
 */
export function DeleteAccountSection({ onDeleted }: { onDeleted: () => void }) {
  const [confirming, setConfirming] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [working, setWorking] = useState(false)

  if (!confirming) {
    return (
      <section className="danger-zone">
        <h2>Radera kontot</h2>
        <p className="state">
          Kontot och allt som hör till det tas bort direkt och går inte att få tillbaka.
        </p>
        <button
          type="button"
          className="button button--danger"
          onClick={() => {
            setConfirming(true)
          }}
        >
          Radera mitt konto
        </button>
      </section>
    )
  }

  return (
    <section className="danger-zone">
      <h2>Är du säker?</h2>

      <p className="state" role="alert">
        Det här tas bort direkt och går inte att ångra: ditt konto, dina samåkningar och dina
        notisinställningar.
      </p>

      <p className="state">
        <strong>Spelarkortet påverkas inte.</strong> Barnets statistik har aldrig legat på servern —
        den ligger kvar i den här telefonen tills du tar bort den själv.
      </p>

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      <div className="actions">
        <button
          type="button"
          className="button"
          onClick={() => {
            setConfirming(false)
            setFailure(null)
          }}
        >
          Avbryt
        </button>

        <button
          type="button"
          className="button button--danger"
          disabled={working}
          onClick={() => {
            void (async () => {
              setWorking(true)

              try {
                await deleteAccount()
                onDeleted()
              } catch (error) {
                setFailure(
                  error instanceof ApiError && error.offline
                    ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
                    : 'Kunde inte radera kontot just nu. Försök igen om en stund.',
                )
              } finally {
                setWorking(false)
              }
            })()
          }}
        >
          {working ? 'Raderar…' : 'Ja, radera kontot'}
        </button>
      </div>
    </section>
  )
}
