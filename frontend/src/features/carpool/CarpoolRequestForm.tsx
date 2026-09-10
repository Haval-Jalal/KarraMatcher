import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'

import { MAX_MESSAGE_LENGTH, MAX_SEATS, type CarpoolRequestInput } from './carpoolApi'

/**
 * Att fråga om en plats.
 *
 * <h3>Går att skicka även när bilen är full</h3>
 *
 * Avsiktligt (§KM.12). Föraren ska kunna svara "någon annan hann före" i stället för att
 * den som frågar möts av en död knapp — men det ska synas i förväg att bilen är full, så
 * att ingen tror att platsen är klar.
 */

const schema = z.object({
  seats: z.number().int().min(1).max(MAX_SEATS),
  message: z.string().max(MAX_MESSAGE_LENGTH, 'Hälsningen är för lång.'),
})

type FormValues = z.infer<typeof schema>

export function CarpoolRequestForm({
  offerId,
  isFull,
  onSubmit,
  onCancel,
}: {
  offerId: string
  isFull: boolean
  onSubmit: (input: CarpoolRequestInput) => Promise<void>
  onCancel: () => void
}) {
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { seats: 1, message: '' },
  })

  // Id:na måste vara unika på sidan: flera erbjudanden kan ha varsitt öppet formulär.
  const seatsId = `platser-${offerId}`
  const messageId = `halsning-${offerId}`

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
      {isFull && (
        <p className="notice" role="status">
          Bilen är full just nu. Du kan fråga ändå — föraren svarar.
        </p>
      )}

      <div className="form__field">
        <label htmlFor={seatsId}>Hur många platser?</label>
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
        <button type="submit" className="button" disabled={isSubmitting}>
          {isSubmitting ? 'Skickar…' : 'Skicka förfrågan'}
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
 * Servern skiljer på "du har redan frågat" och "det är ditt eget erbjudande" (409), och
 * på ett erbjudande som hunnit dras tillbaka (404). Skillnaden är värd att säga: den
 * första betyder vänta, den andra betyder att erbjudandet är borta.
 */
function failureText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.offline) {
      return 'Ingen anslutning. Förfrågan är inte skickad — försök igen när du har nät.'
    }

    if (error.status === 409 || error.status === 404) {
      return error.message
    }
  }

  return 'Förfrågan gick inte att skicka just nu. Försök igen om en stund.'
}
