import { useState } from 'react'

import { decodeBackup, encodeBackup } from './backup/backupCode'
import { describeMerge, mergeCards } from './backup/mergeCards'
import { readCard, writeCard } from './storage/playerCardStore'
import { backupSignature, type PlayerCardData } from './storage/schema'

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
 *
 * <h3>Ägaren håller kortet, inte den här sektionen (`#467`)</h3>
 *
 * Kortet kommer in som prop från <see cref="usePlayerCard"/> på sidan. Tidigare höll
 * sektionen en egen ögonblicksbild (<c>useState(readCard)</c>), som inte uppdaterades när
 * ett barn lades till på samma sida — då kunde "Kopiera koden" koda en tom profil men ändå
 * visa grönt "Säkerhetskopierad". Nu speglar kod och status alltid det aktuella kortet.
 */
export function BackupSection({
  card,
  onChanged,
}: {
  card: PlayerCardData
  onChanged: () => void
}) {
  const [copied, setCopied] = useState(false)
  const [copyFailed, setCopyFailed] = useState(false)
  const [showCode, setShowCode] = useState(false)
  const [pasted, setPasted] = useState('')
  const [outcome, setOutcome] = useState<{ ok: boolean; message: string } | null>(null)

  const hasContent = card.children.length > 0 || card.reports.length > 0
  const neverBackedUp = hasContent && card.lastBackupUtc === null
  // Nya resultat sedan den senaste kopian: innehållet skiljer sig från det som kopierades. Kräver en
  // sparad signatur (äldre kort saknar den — då vet vi inte, och varnar inte falskt) (#596).
  const stale =
    hasContent &&
    card.lastBackupUtc !== null &&
    card.lastBackupSignature != null &&
    card.lastBackupSignature !== backupSignature(card)

  const code = encodeBackup(card)

  return (
    <section className="backup">
      <h2>Säkerhetskopia</h2>

      {!hasContent ? (
        <p className="state" role="status">
          Inget att säkerhetskopiera än. När du fyllt i matcher kan du spara en kopia här.
        </p>
      ) : neverBackedUp ? (
        <p className="state state--error" role="status">
          <strong>Ingen kopia än.</strong> Byter du telefon eller rensar webbläsaren är statistiken
          borta — det finns ingen kopia någon annanstans.
        </p>
      ) : stale ? (
        <p className="state state--error" role="status">
          <strong>Nya resultat sedan din senaste kopia.</strong> Kopiera koden igen så är allt med —
          annars saknas det nya om du byter telefon.
        </p>
      ) : (
        <p className="state" role="status">
          Säkerhetskopierad. Uppdatera kopian när du lagt in nya matcher.
        </p>
      )}

      <div className="actions">
        <button
          type="button"
          className="button button--action"
          onClick={() => {
            void (async () => {
              try {
                await navigator.clipboard.writeText(code)
              } catch {
                // Urklipp kan vara blockerat/osäker kontext/iOS. Stämpla då INTE kopian som gjord:
                // en falsk "kopierad" släcker "Ingen kopia än"-varningen och lämnar familjen utan
                // kopia — raka motsatsen till §KM.2. Öppna i stället koden för manuell kopiering.
                setCopied(false)
                setCopyFailed(true)
                setShowCode(true)

                return
              }

              setCopyFailed(false)

              // Stämpla tid OCH innehålls-signatur: signaturen låter vyn senare säga att nya
              // resultat tillkommit sedan den här kopian (#596).
              const stamped = {
                ...card,
                lastBackupUtc: new Date().toISOString(),
                lastBackupSignature: backupSignature(card),
              }

              writeCard(stamped)
              setCopied(true)
              // Sidan äger kortet: onChanged läser om och skickar ned det stämplade kortet,
              // så status och kod speglar det utan en egen ögonblicksbild här (`#467`).
              onChanged()
            })()
          }}
        >
          Kopiera koden
        </button>
      </div>

      {copied && (
        <p className="state" role="status">
          Koden är kopierad. Spara den någonstans du hittar den igen — en anteckning, ett mejl till
          dig själv, eller en lapp i plånboken.
        </p>
      )}

      {copyFailed && (
        <p className="state state--error" role="alert">
          Koden kunde inte kopieras automatiskt på den här enheten. Markera koden nedan under{' '}
          <strong>Visa koden</strong> och kopiera den för hand — annars finns ingen kopia.
        </p>
      )}

      {/* Koden är lång och rörs sällan — göms tills man vill se eller kopiera den för hand. Öppnas
          automatiskt om den automatiska kopieringen misslyckas (#535), men går att stänga igen. */}
      <details
        className="backup__panel"
        open={showCode}
        onToggle={(event) => {
          setShowCode(event.currentTarget.open)
        }}
      >
        <summary>Visa koden</summary>
        <label className="form__field" htmlFor="backupkod">
          <span className="visually-hidden">Din säkerhetskopieringskod</span>
          <textarea id="backupkod" readOnly rows={3} value={code} />
        </label>
      </details>

      <details className="backup__panel">
        <summary>Återställ från en kod</summary>

        <p className="admin-muted">
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
      </details>
    </section>
  )
}
