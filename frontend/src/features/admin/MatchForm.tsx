import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import type { Match } from '@/features/matches'
import { ApiError } from '@/lib/api'
import { swedishLocalToUtc, utcToSwedishLocalInput } from '@/lib/time'

import { searchVenues, type MatchInput, type Venue } from './adminApi'

/**
 * Tränarens matchformulär.
 *
 * <h3>Tiden skrivs i svensk tid och sparas i UTC</h3>
 *
 * Tränaren skriver "14:00" och menar 14:00 på planen. Omräkningen sker i `lib/time.ts`,
 * som är frontendens enda ställe där UTC möter svensk tid (§KM.5) — här görs den bara,
 * aldrig på egen hand.
 *
 * <h3>Vad som är utelämnat med flit</h3>
 *
 * Inget fält för koordinater. De härleds ur spelplatsens adress när platsen läggs upp,
 * eftersom handinmatade koordinater visade sig ligga upp till 2,2 km fel (`#110`).
 */

const schema = z.object({
  kickoffLocal: z
    .string()
    .min(1, 'Fyll i datum och tid.')
    .refine((value) => swedishLocalToUtc(value) !== null, 'Datum och tid ser inte riktiga ut.'),
  opponent: z.string().trim().min(1, 'Fyll i motståndarlaget.').max(120, 'Namnet är för långt.'),
  venueId: z.string().min(1, 'Välj en spelplats.'),
  isHome: z.boolean(),
  note: z.string().max(500, 'Notisen är för lång.'),
})

type FormValues = z.infer<typeof schema>

export function MatchForm({
  existing,
  onSubmit,
  onCancel,
}: {
  existing?: Match
  onSubmit: (input: MatchInput) => Promise<void>
  onCancel: () => void
}) {
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      kickoffLocal: existing ? utcToSwedishLocalInput(existing.kickoffUtc) : '',
      opponent: existing?.opponent ?? '',
      venueId: '',
      isHome: existing?.isHome ?? true,
      note: '',
    },
  })

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async (values) => {
          const kickoffUtc = swedishLocalToUtc(values.kickoffLocal)

          if (kickoffUtc === null) {
            setFailure('Datum och tid ser inte riktiga ut.')

            return
          }

          try {
            await onSubmit({
              kickoffUtc,
              opponent: values.opponent.trim(),
              venueId: values.venueId,
              isHome: values.isHome,
              note: values.note.trim() === '' ? null : values.note.trim(),
            })
            setFailure(null)
          } catch (error) {
            setFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
                : 'Matchen gick inte att spara just nu. Försök igen om en stund.',
            )
          }
        })(event)
      }}
    >
      <div className="form__field">
        <label htmlFor="avspark">Avspark (svensk tid)</label>
        <input
          id="avspark"
          type="datetime-local"
          aria-describedby={errors.kickoffLocal ? 'avspark-fel' : undefined}
          aria-invalid={errors.kickoffLocal ? true : undefined}
          {...register('kickoffLocal')}
        />
        {errors.kickoffLocal && (
          <p className="form__error" id="avspark-fel">
            {errors.kickoffLocal.message}
          </p>
        )}
      </div>

      <div className="form__field">
        <label htmlFor="motstandare">Motståndare</label>
        <input
          id="motstandare"
          type="text"
          autoComplete="off"
          aria-describedby={errors.opponent ? 'motstandare-fel' : undefined}
          aria-invalid={errors.opponent ? true : undefined}
          {...register('opponent')}
        />
        {errors.opponent && (
          <p className="form__error" id="motstandare-fel">
            {errors.opponent.message}
          </p>
        )}
      </div>

      <VenuePicker
        {...(errors.venueId?.message === undefined ? {} : { error: errors.venueId.message })}
        onSelect={(venue) => {
          setValue('venueId', venue.id, { shouldValidate: true })
          setValue('isHome', venue.isHome)
        }}
      />

      <div className="form__field">
        <label htmlFor="notis">Notis till föräldrarna (valfritt)</label>
        <input id="notis" type="text" autoComplete="off" {...register('note')} />
      </div>

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      <div className="actions">
        <button type="submit" className="button" disabled={isSubmitting}>
          {isSubmitting ? 'Sparar…' : existing ? 'Spara ändringen' : 'Lägg till matchen'}
        </button>
        <button type="button" className="button" onClick={onCancel}>
          Avbryt
        </button>
      </div>
    </form>
  )
}

/**
 * Spelplats med förslag medan man skriver.
 *
 * <para>
 * Förslagen kommer ur registret och inte ur fritext. En felstavad plats bryter både
 * vägbeskrivningen och väderprognosen, så tränaren väljer alltid en befintlig plats —
 * nya läggs upp i spelplatsregistret, där adressen geokodas.
 * </para>
 */
function VenuePicker({ error, onSelect }: { error?: string; onSelect: (venue: Venue) => void }) {
  const [term, setTerm] = useState('')
  const [options, setOptions] = useState<Venue[]>([])
  const [chosen, setChosen] = useState<Venue | null>(null)

  useEffect(() => {
    let cancelled = false

    /*
     * Kort fordrojning innan uppslagningen: tranaren skriver pa en telefon, och ett anrop
     * per tangenttryckning ar bade langsamt och onodigt.
     */
    const timer = setTimeout(() => {
      void searchVenues(term)
        .then((found) => {
          if (!cancelled) {
            setOptions(found)
          }
        })
        .catch(() => {
          // Uteblivna förslag är en olägenhet, inte ett fel att avbryta formuläret för.
          if (!cancelled) {
            setOptions([])
          }
        })
    }, 250)

    return () => {
      cancelled = true
      clearTimeout(timer)
    }
  }, [term])

  return (
    <div className="form__field">
      <label htmlFor="spelplats">Spelplats</label>

      <input
        id="spelplats"
        type="text"
        autoComplete="off"
        role="combobox"
        aria-expanded={options.length > 0}
        aria-controls="spelplats-forslag"
        aria-describedby={error ? 'spelplats-fel' : undefined}
        aria-invalid={error ? true : undefined}
        value={chosen === null ? term : chosen.name}
        onChange={(event) => {
          setChosen(null)
          setTerm(event.target.value)
        }}
      />

      {chosen === null && options.length > 0 && (
        <ul className="suggestions" id="spelplats-forslag">
          {options.map((venue) => (
            <li key={venue.id}>
              <button
                type="button"
                className="suggestions__option"
                onClick={() => {
                  setChosen(venue)
                  onSelect(venue)
                }}
              >
                <span className="suggestions__name">{venue.name}</span>
                <span className="suggestions__address">{venue.address}</span>
              </button>
            </li>
          ))}
        </ul>
      )}

      {chosen !== null && (
        <p className="state">
          Vald: {chosen.name} — {chosen.address}
        </p>
      )}

      {error !== undefined && (
        <p className="form__error" id="spelplats-fel">
          {error}
        </p>
      )}
    </div>
  )
}
