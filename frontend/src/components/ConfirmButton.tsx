import { useState, type ReactNode } from 'react'

/**
 * En knapp för en åtgärd som inte går att ångra: första klicket beväpnar, ett andra bekräftar
 * (`#383`). Ett oavsiktligt tryck raderar då inte en match ur föräldrarnas kalendrar, tystar en
 * medlem eller nekar en ansökan — man måste bekräfta först.
 *
 * <h3>Tillgänglighet</h3>
 *
 * När knappen beväpnas flyttas fokus till "Bekräfta" (autoFocus), så tangentbord och skärmläsare
 * hamnar på det som händer härnäst. "Avbryt" tar tillbaka. Bekräftelsen är en `role="group"` med
 * ett namn, så det framgår att de två knapparna hör ihop.
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
}) {
  const [armed, setArmed] = useState(false)

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
      <button
        type="button"
        className={confirmClassName}
        disabled={disabled}
        autoFocus
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
