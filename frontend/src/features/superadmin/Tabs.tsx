import { useId, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'

/** En flik: en stabil nyckel, en etikett och innehållet som visas när den är vald. */
export interface TabDefinition {
  key: string
  label: string
  panel: ReactNode
}

/**
 * Flikar för superadmin-konsolen (`#251`).
 *
 * <h3>En sektion i taget</h3>
 *
 * Konsolen har fyra tunga sektioner. Staplade och alla synliga blev de en vägg; bakom flikar
 * ser man en i taget. Alla paneler ligger kvar i DOM:en (dolda med <c>hidden</c>) så att ett
 * halvifyllt formulär inte försvinner när man råkar byta flik och tillbaka.
 *
 * <h3>Tangentbord (§KM.0 A3)</h3>
 *
 * Mönstret följer WAI-ARIA: <c>tablist</c>/<c>tab</c>/<c>tabpanel</c>, bara den valda fliken i
 * tabbordningen (roving tabindex), och vänster/höger/Home/End flyttar mellan flikarna. En
 * skärmläsare läser "flik 2 av 4, vald" i stället för fyra namnlösa knappar.
 */
export function Tabs({ tabs, label }: { tabs: TabDefinition[]; label: string }) {
  const [active, setActive] = useState(0)
  const base = useId()
  const buttons = useRef<(HTMLButtonElement | null)[]>([])

  function onKeyDown(event: KeyboardEvent<HTMLButtonElement>): void {
    const last = tabs.length - 1
    let next: number | null = null

    if (event.key === 'ArrowRight') {
      next = active === last ? 0 : active + 1
    } else if (event.key === 'ArrowLeft') {
      next = active === 0 ? last : active - 1
    } else if (event.key === 'Home') {
      next = 0
    } else if (event.key === 'End') {
      next = last
    }

    if (next !== null) {
      event.preventDefault()
      setActive(next)
      buttons.current[next]?.focus()
    }
  }

  return (
    <div className="tabs">
      <div className="tabs__list" role="tablist" aria-label={label}>
        {tabs.map((tab, index) => (
          <button
            key={tab.key}
            ref={(element) => {
              buttons.current[index] = element
            }}
            id={`${base}-tab-${tab.key}`}
            type="button"
            role="tab"
            aria-selected={index === active}
            aria-controls={`${base}-panel-${tab.key}`}
            tabIndex={index === active ? 0 : -1}
            className={index === active ? 'tabs__tab tabs__tab--active' : 'tabs__tab'}
            onClick={() => {
              setActive(index)
            }}
            onKeyDown={onKeyDown}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {tabs.map((tab, index) => (
        <div
          key={tab.key}
          id={`${base}-panel-${tab.key}`}
          role="tabpanel"
          aria-labelledby={`${base}-tab-${tab.key}`}
          hidden={index !== active}
        >
          {tab.panel}
        </div>
      ))}
    </div>
  )
}
