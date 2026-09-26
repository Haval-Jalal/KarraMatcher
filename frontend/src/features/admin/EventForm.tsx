import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { useClubVenue } from '@/features/clubs/useClubVenue'
import type { TeamEvent } from '@/features/events'
import { ApiError } from '@/lib/api'
import { swedishLocalToUtc, utcToSwedishLocalInput } from '@/lib/time'

import type { EventInput } from './adminApi'

/**
 * Tränarens formulär för en händelse — match, träning, cup eller övrigt (`#198`, `#307`).
 *
 * <h3>Typen styr fälten</h3>
 *
 * En match har motståndare; en träning/cup/övrig händelse en rubrik. Väljaren högst upp
 * bestämmer vilka fält som visas.
 *
 * <h3>Plats: hemma eller annan plats</h3>
 *
 * <b>Hemma</b> använder klubbens hemmaplan (adressen fylls i automatiskt server-side). <b>Annan
 * plats</b> — en bortamatch eller t.ex. en vinterträning inomhus — låter tränaren skriva
 * adressen, som geokodas. Gäller alla typer (`#307`).
 *
 * <h3>Tiden skrivs i svensk tid och sparas i UTC</h3>
 *
 * Omräkningen sker i `lib/time.ts`, frontendens enda ställe där UTC möter svensk tid (§KM.5).
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
    address: z.string().max(200, 'Adressen är för lång.'),
    title: z.string().max(120, 'Rubriken är för lång.'),
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

    if (!values.isHome && values.address.trim() === '') {
      ctx.addIssue({ path: ['address'], code: 'custom', message: 'Skriv adressen till platsen.' })
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
  truppId,
  existing,
  onSubmit,
  onCancel,
}: {
  truppId: string
  existing?: TeamEvent
  onSubmit: (input: EventInput) => Promise<void>
  onCancel: () => void
}) {
  const [failure, setFailure] = useState<string | null>(null)
  const club = useClubVenue(truppId)

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
      address: existing && existing.isHome === false ? existing.address : '',
      title: existing?.title ?? '',
      note: '',
    },
  })

  // Typen och hemma/borta styr vilka fält som visas. Hålls i lokal state (React Compiler kan
  // inte memoisera RHF:s watch() säkert); setValue håller formulärvärdet i takt.
  const [type, setType] = useState<FormValues['type']>(existing?.type ?? 'Match')
  const [isHome, setIsHome] = useState(existing?.isHome ?? true)
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
              isHome: values.isHome,
              address: values.isHome ? null : values.address.trim(),
              note: values.note.trim() === '' ? null : values.note.trim(),
            })
            setFailure(null)
          } catch (error) {
            setFailure(
              error instanceof ApiError && !error.offline
                ? error.message
                : 'Ingen anslutning. Kontrollera nätet och försök igen.',
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

      {/* Plats: hemma (klubbens plan) eller annan plats (skriven adress, geokodas) (`#307`). */}
      <fieldset className="form__field">
        <legend>Plats</legend>

        <label className="form__radio">
          <input
            type="radio"
            name="plats"
            checked={isHome}
            onChange={() => {
              setIsHome(true)
              setValue('isHome', true, { shouldValidate: true })
            }}
          />{' '}
          {isMatch ? 'Hemmamatch' : 'Hemma'} (klubbens plan)
        </label>

        <label className="form__radio">
          <input
            type="radio"
            name="plats"
            checked={!isHome}
            onChange={() => {
              setIsHome(false)
              setValue('isHome', false, { shouldValidate: true })
            }}
          />{' '}
          {isMatch ? 'Bortamatch' : 'Annan plats'}
        </label>

        {isHome ? (
          club.data?.configured ? (
            <p className="state">
              Klubbens plan: {club.data.name} — {club.data.address}
            </p>
          ) : (
            <p className="state state--error" role="alert">
              Klubben har ingen hemmaplan ännu. Sätt den under Inställningar innan du lägger upp en
              hemma-aktivitet.
            </p>
          )
        ) : (
          <div className="form__field">
            <label htmlFor="adress">Adress</label>
            <input
              id="adress"
              type="text"
              autoComplete="off"
              placeholder="t.ex. Bortavägen 5, Kungälv"
              aria-describedby={errors.address ? 'adress-fel' : undefined}
              aria-invalid={errors.address ? true : undefined}
              {...register('address')}
            />
            <p className="admin-muted">Skriv gatunamn och ort så hittas rätt plats.</p>
            {errors.address && (
              <p className="form__error" id="adress-fel">
                {errors.address.message}
              </p>
            )}
          </div>
        )}
      </fieldset>

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
