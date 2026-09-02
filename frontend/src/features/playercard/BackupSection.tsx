import { useState } from 'react'

import { decodeBackup, encodeBackup } from './backup/backupCode'
import { describeMerge, mergeCards } from './backup/mergeCards'
import { readCard, writeCard } from './storage/playerCardStore'
import type { PlayerCardData } from './storage/schema'

/**
 * Säkerhetskopiering av spelarkortet.
 *
 * <h3>Varför den ligger synlig och inte under en inställning</h3>
 *
 * Kortet finns bara på telefonen (§KM.2). Koden är det enda som står mellan en familj och
 * en förlorad säsong vid ett telefonbyte, och en funktion man måste leta efter är en
 * funktion ingen använder förrän det är för sent.
 *
 * Påminnelsen visas när kortet har innehåll men aldrig kopierats — inte som en varning,
 * utan som ett konstaterande av vad som händer om telefonen byts i det läget.
 */
export function BackupSection({ onChanged }: { onChanged: () => void }) {
  const [card, setCard] = useState<PlayerCardData>(readCard)
  const [copied, setCopied] = useState(false)
  const [pasted, setPasted] = useState('')
  const [outcome, setOutcome] = useState<{ ok: boolean; message: string } | null>(null)

  const hasContent = card.children.length > 0 || card.reports.length > 0
  const neverBackedUp = hasContent && card.lastBackupUtc === null

  const code = encodeBackup(card)

  return (
    <section className="backup">
      <h2>Säkerhetskopia</h2>

      {neverBackedUp && (
        <p className="state state--error" role="status">
          <strong>Du har inte sparat någon kod än.</strong> Byter du telefon, eller rensar
          webbläsarens data, är statistiken borta — det finns ingen kopia någon annanstans.
        </p>
      )}

      <p className="state">
        Koden innehåller barnen och deras matchrapporter. Spara den där du hittar den igen — i en
        anteckning, ett mejl till dig själv, eller en lapp i plånboken.
      </p>

      <label className="form__field" htmlFor="backupkod">
        <span>Din kod</span>
        <textarea id="backupkod" readOnly rows={3} value={code} />
      </label>

      <div className="actions">
        <button
          type="button"
          className="button"
          onClick={() => {
            void (async () => {
              try {
                await navigator.clipboard.writeText(code)
              } catch {
                // Urklipp kan vara blockerat. Koden står redan i fältet ovan, så den går
                // att markera och kopiera för hand — knappen är genvägen, inte enda vägen.
              }

              const stamped = { ...card, lastBackupUtc: new Date().toISOString() }

              writeCard(stamped)
              setCard(stamped)
              setCopied(true)
              onChanged()
            })()
          }}
        >
          Kopiera koden
        </button>
      </div>

      {copied && (
        <p className="state" role="status">
          Koden är kopierad. Klistra in den någonstans du hittar den igen.
        </p>
      )}

      <h3>Återställ från en kod</h3>

      <p className="state">
        Import <strong>lägger till</strong> — det som redan finns på den här telefonen rörs inte.
      </p>

      <label className="form__field" htmlFor="importkod">
        <span>Klistra in en kod</span>
        <textarea
          id="importkod"
          rows={3}
          value={pasted}
          onChange={(event) => {
            setPasted(event.target.value)
            setOutcome(null)
          }}
        />
      </label>

      <div className="actions">
        <button
          type="button"
          className="button"
          onClick={() => {
            const result = decodeBackup(pasted)

            if (!result.ok) {
              setOutcome({ ok: false, message: result.reason })

              return
            }

            const before = readCard()
            const merged = mergeCards(before, result.card)

            writeCard(merged)
            setCard(merged)
            setPasted('')
            setOutcome({
              ok: true,
              message:
                describeMerge(before, merged) +
                (result.legacy ? ' Koden kom från den gamla appen.' : ''),
            })
            onChanged()
          }}
        >
          Återställ
        </button>
      </div>

      {outcome !== null && (
        <p
          className={outcome.ok ? 'state' : 'state state--error'}
          role={outcome.ok ? 'status' : 'alert'}
        >
          {outcome.message}
        </p>
      )}
    </section>
  )
}
