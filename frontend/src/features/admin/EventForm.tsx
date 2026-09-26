import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import type { TeamEvent } from '@/features/events'
import { ApiError } from '@/lib/api'
import { swedishLocalToUtc, utcToSwedishLocalInput } from '@/lib/time'

import { searchVenues, type EventInput, type Venue } from './adminApi'

/**
 * Tränarens formulär för en händelse — match, träning eller övrigt (`#198`).
 *
 * <h3>Typen styr fälten</h3>
 *
 * En match har motståndare och hemma/borta; en träning eller övrig händelse har i stället
 * en rubrik. Väljaren högst upp bestämmer vilka fält som visas, och valideringen kräver rätt
 * fält för rätt typ.
 *
 * <h3>Tiden skrivs i svensk tid och sparas i UTC</h3>
 *
 * Omräkningen sker i `lib/time.ts`, frontendens enda ställe där UTC möter svensk tid (§KM.5).
 *
 * <h3>Vad som är utelämnat med flit</h3>
 *
 * Inget fält för koordinater — de härleds ur spelplatsens adress (`#110`).
 */

const schema = z
  .object({
    type: z.enum(['Match', 'Training', 'Other', 'Cup']),
    kickoffLocal: z
      .string()
      .min(1, 'Fyll i datum och tid.')
      .refine((value) => swedishLocalToUtc(value) !== null, 'Datum och tid ser inte riktiga ut.'),
    opponent: z.string().max(120, 'Namnet är för långt.'),
    isHome: z.boolean(),
    title: z.string().max(120, 'Rubriken är för lång.'),
    venueId: z.string().min(1, 'Välj en spelplats.'),
    note: z.string().max(500, 'Notisen är för lång.'),
  })
  .superRefine((values, ctx) => {
    if (values.type === 'Match') {
      if (values.opponent.trim() === '') {
        ctx.addIssue({ path: ['opponent'], code: 'custom', message: 'Fyll i motståndarlaget.' })
      }
    } else if (values.title.trim() === '') {
      ctx.addIssue({ path: ['title'], code: 'custom', message: 'Fyll i en rubrik.' })
    }
  })

type FormValues = z.infer<typeof schema>

const TYPE_LABELS: Record<FormValues['type'], string> = {
  Match: 'Match',
  Training: 'Träning',
  Cup: 'Cup',
  Other: 'Övrigt',
}

export function EventForm({
  existing,
  onSubmit,
  onCancel,
}: {
  existing?: TeamEvent
  onSubmit: (input: EventInput) => Promise<void>
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
      type: existing?.type ?? 'Match',
      kickoffLocal: existing ? utcToSwedishLocalInput(existing.kickoffUtc) : '',
      opponent: existing?.opponent ?? '',
      isHome: existing?.isHome ?? true,
      title: existing?.title ?? '',
      venueId: '',
      note: '',
    },
  })

  // Typen styr vilka fält som visas. Hålls i lokal state i stället för RHF:s watch(), som
  // React Compiler inte kan memoisera säkert; setValue håller formulärvärdet i takt.
  const [type, setType] = useState<FormValues['type']>(existing?.type ?? 'Match')
  const isMatch = type === 'Match'

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

          const matchType = values.type === 'Match'

          try {
            await onSubmit({
              type: values.type,
              kickoffUtc,
              title: matchType ? null : values.title.trim(),
              opponent: matchType ? values.opponent.trim() : null,
              venueId: values.venueId,
              isHome: matchType ? values.isHome : null,
              note: values.note.trim() === '' ? null : values.note.trim(),
            })
            setFailure(null)
          } catch (error) {
            setFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Kontrollera nätet och försök igen.'
                : 'Händelsen gick inte att spara just nu. Försök igen om en stund.',
            )
          }
        })(event)
      }}
    >
      <div className="form__field">
        <label htmlFor="handelsetyp">Typ</label>
        <select
          id="handelsetyp"
          value={type}
          onChange={(event) => {
            const next = event.target.value as FormValues['type']
            setType(next)
            setValue('type', next, { shouldValidate: false })
          }}
        >
          {(['Match', 'Training', 'Cup', 'Other'] as const).map((value) => (
            <option key={value} value={value}>
              {TYPE_LABELS[value]}
            </option>
          ))}
        </select>
      </div>

      <div className="form__field">
        <label htmlFor="avspark">{isMatch ? 'Avspark (svensk tid)' : 'Start (svensk tid)'}</label>
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

      {isMatch ? (
        <>
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

          <div className="form__field form__field--checkbox">
            <label htmlFor="hemma">
              <input id="hemma" type="checkbox" {...register('isHome')} /> Hemmamatch
            </label>
          </div>
        </>
      ) : (
        <div className="form__field">
          <label htmlFor="rubrik">Rubrik</label>
          <input
            id="rubrik"
            type="text"
            autoComplete="off"
            aria-describedby={errors.title ? 'rubrik-fel' : undefined}
            aria-invalid={errors.title ? true : undefined}
            {...register('title')}
          />
          {errors.title && (
            <p className="form__error" id="rubrik-fel">
              {errors.title.message}
            </p>
          )}
        </div>
      )}

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
          {isSubmitting ? 'Sparar…' : existing ? 'Spara ändringen' : 'Lägg till händelsen'}
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
 * Förslagen kommer ur registret och inte ur fritext. En felstavad plats bryter både
 * vägbeskrivningen och väderprognosen, så tränaren väljer alltid en befintlig plats —
 * nya läggs upp i spelplatsregistret, där adressen geokodas.
 */
function VenuePicker({ error, onSelect }: { error?: string; onSelect: (venue: Venue) => void }) {
  const [term, setTerm] = useState('')
  const [options, setOptions] = useState<Venue[]>([])
  const [chosen, setChosen] = useState<Venue | null>(null)

  useEffect(() => {
    let cancelled = false

    const timer = setTimeout(() => {
      void searchVenues(term)
        .then((found) => {
          if (!cancelled) {
            setOptions(found)
          }
        })
        .catch(() => {
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
