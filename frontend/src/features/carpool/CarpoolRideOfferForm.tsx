import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'

import { MAX_MESSAGE_LENGTH, MAX_SEATS, type CarpoolRideOfferInput } from './carpoolApi'

/**
 * Att erbjuda plats på en skjutsförfrågan (§KM.12, `#63`).
 *
 * Spegelbilden av att fråga om plats: här är det föraren som räcker upp handen, och föräldern som
 * frågade svarar. Antalet platser står för hur många föraren kan ta, och hälsningen är valfri —
 * överenskommelsen sker där, appen frågar aldrig efter ett telefonnummer.
 */

const schema = z.object({
  seats: z.number().int().min(1).max(MAX_SEATS),
  message: z.string().max(MAX_MESSAGE_LENGTH, 'Hälsningen är för lång.'),
})

type FormValues = z.infer<typeof schema>

export function CarpoolRideOfferForm({
  rideRequestId,
  onSubmit,
  onCancel,
}: {
  rideRequestId: string
  onSubmit: (input: CarpoolRideOfferInput) => Promise<void>
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
    defaultValues: { seats: 1, message: '' },
  })

  // Formuläret ersätter "Erbjud plats"-knappen; flytta fokus hit (WCAG 2.4.3, #598).
  useEffect(() => {
    setFocus('seats')
  }, [setFocus])

  // Id:na måste vara unika på sidan: flera förfrågningar kan ha varsitt öppet formulär.
  const seatsId = `erbjuder-platser-${rideRequestId}`
  const messageId = `erbjuder-halsning-${rideRequestId}`

  return (
    <form
      className="form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async (values) => {
          try {
            await onSubmit({
              seats: values.seats,
              message: values.message.trim() === '' ? null : values.message.trim(),
            })
            setFailure(null)
          } catch (error) {
            setFailure(failureText(error))
          }
        })(event)
      }}
    >
      <div className="form__field">
        <label htmlFor={seatsId}>Hur många platser kan du ta?</label>
        {/* valueAsNumber: en <select> ger sträng, och schemat vill ha ett tal. */}
        <select id={seatsId} {...register('seats', { valueAsNumber: true })}>
          {Array.from({ length: MAX_SEATS }, (_, index) => index + 1).map((seats) => (
            <option key={seats} value={seats}>
              {seats}
            </option>
          ))}
        </select>
      </div>

      <div className="form__field">
        <label htmlFor={messageId}>Hälsning (valfritt)</label>
        <textarea
          id={messageId}
          rows={2}
          aria-describedby={errors.message ? `${messageId}-fel` : undefined}
          aria-invalid={errors.message ? true : undefined}
          {...register('message')}
        />
        {errors.message && (
          <p className="form__error" id={`${messageId}-fel`}>
            {errors.message.message}
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
          {isSubmitting ? 'Skickar…' : 'Erbjud plats'}
        </button>
        <button type="button" className="button button--action" onClick={onCancel}>
          Avbryt
        </button>
      </div>
    </form>
  )
}

/**
 * Vad som gick fel, sagt så att en förälder vet vad som gäller.
 *
 * Servern skiljer på "du har redan erbjudit plats" och "det är din egen förfrågan" (409), och på en
 * förfrågan som hunnit lösas eller dras tillbaka (404). Den första betyder vänta, den andra att
 * förfrågan är borta.
 */
function failureText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.offline) {
      return 'Ingen anslutning. Erbjudandet är inte skickat — försök igen när du har nät.'
    }

    if (error.status === 409 || error.status === 404) {
      return error.message
    }
  }

  return 'Erbjudandet gick inte att skicka just nu. Försök igen om en stund.'
}
