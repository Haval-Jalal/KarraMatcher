import { useId, useState, type ReactNode } from 'react'

/**
 * En knapp för en åtgärd som inte går att ångra: första klicket beväpnar, ett andra bekräftar
 * (`#383`). Ett oavsiktligt tryck raderar då inte en match ur föräldrarnas kalendrar, tystar en
 * medlem eller nekar en ansökan — man måste bekräfta först.
 *
 * <h3>Tillgänglighet</h3>
 *
 * När knappen beväpnas flyttas fokus till "Bekräfta" (autoFocus), så tangentbord och skärmläsare
 * hamnar på det som händer härnäst. "Avbryt" tar tillbaka. Bekräftelsen är en `role="group"` med
 * ett namn, så det framgår att de två knapparna hör ihop. En valfri `confirmHint` beskriver vad
 * som händer vid bekräftelse och knyts till knappen via `aria-describedby`.
 *
 * <h3>Distinkt bekräftelse-etikett</h3>
 *
 * Ge `confirmLabel` en egen text ("Ja, ställ in") när den vilande knappen annars hade sett
 * likadan ut — annars märks det inte att man gått från vilande till beväpnad på en snabb
 * dubbelklickning (`#606`).
 */
export function ConfirmButton({
  onConfirm,
  label,
  confirmLabel = 'Bekräfta',
  cancelLabel = 'Avbryt',
  className = 'button',
  confirmClassName = 'button button--danger',
  disabled = false,
  ariaLabel,
  confirmHint,
}: {
  onConfirm: () => void
  /** Vad den vilande knappen visar (text eller ikon + dold text). */
  label: ReactNode
  confirmLabel?: string
  cancelLabel?: string
  className?: string
  confirmClassName?: string
  disabled?: boolean
  /** Tillgängligt namn för den vilande knappen när `label` är en ikon. */
  ariaLabel?: string
  /** En rad konsekvens-copy som visas när knappen beväpnats (`#606`). */
  confirmHint?: ReactNode
}) {
  const [armed, setArmed] = useState(false)
  const hintId = useId()

  if (!armed) {
    return (
      <button
        type="button"
        className={className}
        disabled={disabled}
        aria-label={ariaLabel}
        onClick={() => {
          setArmed(true)
        }}
      >
        {label}
      </button>
    )
  }

  return (
    <span className="confirm-inline" role="group" aria-label="Bekräfta åtgärden">
      {confirmHint !== undefined && (
        <span className="confirm-inline__hint" id={hintId}>
          {confirmHint}
        </span>
      )}
      <button
        type="button"
        className={confirmClassName}
        disabled={disabled}
        autoFocus
        aria-describedby={confirmHint !== undefined ? hintId : undefined}
        onClick={() => {
          setArmed(false)
          onConfirm()
        }}
      >
        {confirmLabel}
      </button>
      <button
        type="button"
        className="button button--small"
        onClick={() => {
          setArmed(false)
        }}
      >
        {cancelLabel}
      </button>
    </span>
  )
}
