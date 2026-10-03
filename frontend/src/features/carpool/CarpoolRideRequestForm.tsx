import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'

import {
  MAX_MESSAGE_LENGTH,
  MAX_SEATS,
  type CarpoolDirection,
  type CarpoolRideRequestInput,
} from './carpoolApi'

/**
 * Att be om skjuts (§KM.12, `#63`).
 *
 * <h3>Spegelbilden av att erbjuda</h3>
 *
 * En förälder som saknar skjuts ska inte behöva vänta på att någon lägger upp ett erbjudande.
 * Formuläret frågar bara efter det som behövs: vilket håll man behöver åka, hur många som ska med,
 * och en valfri rad. Inget namn, inget telefonnummer — överenskommelsen sker sedan i meddelandet.
 */

const schema = z.object({
  direction: z.enum(['ToMatch', 'FromMatch', 'Both']),
  seats: z.number().int().min(1).max(MAX_SEATS),
  note: z.string().max(MAX_MESSAGE_LENGTH, 'Notisen är för lång.'),
})

type FormValues = z.infer<typeof schema>

const DIRECTIONS: { value: CarpoolDirection; label: string }[] = [
  { value: 'ToMatch', label: 'Till matchen' },
  { value: 'FromMatch', label: 'Hem från matchen' },
  { value: 'Both', label: 'Båda hållen' },
]

export function CarpoolRideRequestForm({
  onSubmit,
  onCancel,
}: {
  onSubmit: (input: CarpoolRideRequestInput) => Promise<void>
  onCancel: () => void
}) {
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setFocus,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { direction: 'Both', seats: 1, note: '' },
  })

  // Formuläret ersätter "Fråga om skjuts"-knappen; flytta fokus hit (WCAG 2.4.3, #598).
  useEffect(() => {
    setFocus('direction')
  }, [setFocus])

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async (values) => {
          try {
            await onSubmit({
              direction: values.direction,
              seats: values.seats,
              note: values.note.trim() === '' ? null : values.note.trim(),
            })
            setFailure(null)
          } catch (error) {
            setFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Förfrågan är inte sparad — försök igen när du har nät.'
                : 'Förfrågan gick inte att spara just nu. Försök igen om en stund.',
            )
          }
        })(event)
      }}
    >
      <fieldset className="form__field">
        <legend>Vilket håll behöver du skjuts?</legend>
        {DIRECTIONS.map(({ value, label }) => (
          <label key={value} className="form__choice">
            <input type="radio" value={value} {...register('direction')} />
            {label}
          </label>
        ))}
      </fieldset>

      <div className="form__field">
        <label htmlFor="behover-platser">Hur många behöver plats?</label>
        {/* valueAsNumber: en <select> ger sträng, och schemat vill ha ett tal. */}
        <select id="behover-platser" {...register('seats', { valueAsNumber: true })}>
          {Array.from({ length: MAX_SEATS }, (_, index) => index + 1).map((seats) => (
            <option key={seats} value={seats}>
              {seats}
            </option>
          ))}
        </select>
      </div>

      <div className="form__field">
        <label htmlFor="behover-notis">Något mer att säga? (valfritt)</label>
        <textarea
          id="behover-notis"
          rows={2}
          placeholder="T.ex. Vi bor vid Kärra centrum"
          aria-describedby={errors.note ? 'behover-notis-fel' : undefined}
          aria-invalid={errors.note ? true : undefined}
          {...register('note')}
        />
        {errors.note && (
          <p className="form__error" id="behover-notis-fel">
            {errors.note.message}
          </p>
        )}
      </div>

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      <div className="actions">
        <button type="submit" className="button button--action" disabled={isSubmitting}>
          {isSubmitting ? 'Frågar…' : 'Fråga om skjuts'}
        </button>
        <button type="button" className="button" onClick={onCancel}>
          Avbryt
        </button>
      </div>
    </form>
  )
}
