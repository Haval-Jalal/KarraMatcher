import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'

import { ApiError } from '@/lib/api'

import {
  MAX_ATTENDANCE_COUNT,
  submitAttendanceResponse,
  type AttendanceResponse,
  type AttendanceStatus,
} from './attendanceApi'

/**
 * Den vuxnas svar på en kallelse (`#57`, §KM.7).
 *
 * <h3>Ett antal, aldrig ett barn</h3>
 *
 * Man svarar för sin familj: Kommer / Kan inte / Kanske, och hur många. Inget barn nämns —
 * det vet bara den egna telefonen (§KM.1).
 *
 * <h3>"Kan inte" behöver inget antal</h3>
 *
 * Antalsväljaren döljs då, eftersom noll är det enda rimliga och en väljare för det bara är
 * en fråga utan svar. Backend tvingar ändå antalet till noll för ett nej.
 */

const STATUSES: { value: AttendanceStatus; label: string }[] = [
  { value: 'Coming', label: 'Kommer' },
  { value: 'CantCome', label: 'Kan inte' },
  { value: 'Maybe', label: 'Kanske' },
]

const schema = z
  .object({
    status: z.enum(['Coming', 'CantCome', 'Maybe']),
    count: z.number().int().min(0).max(MAX_ATTENDANCE_COUNT),
  })
  .refine((value) => value.status !== 'Coming' || value.count >= 1, {
    message: 'Ange hur många som kommer.',
    path: ['count'],
  })

type FormValues = z.infer<typeof schema>

export function AttendanceResponseForm({
  matchId,
  current,
  onSubmitted,
}: {
  matchId: string
  current: AttendanceResponse | null
  onSubmitted: () => Promise<void>
}) {
  const [failure, setFailure] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      status: current?.status ?? 'Coming',
      count: current?.count ?? 1,
    },
  })

  // useWatch och inte form.watch: det förra ger ett värde, det senare en funktion som
  // React Compiler inte kan memoisera — och då hoppar den över hela komponenten.
  const status = useWatch({ control, name: 'status' })
  const showCount = status !== 'CantCome'

  return (
    <form
      className="form attendance__form"
      noValidate
      onSubmit={(event) => {
        void handleSubmit(async (values) => {
          try {
            await submitAttendanceResponse(
              matchId,
              values.status,
              // "Kan inte" ar noll oavsett vad valjaren stod pa nar den doldes.
              values.status === 'CantCome' ? 0 : values.count,
            )
            setFailure(null)
            await onSubmitted()
          } catch (error) {
            setFailure(
              error instanceof ApiError && error.offline
                ? 'Ingen anslutning. Svaret är inte sparat — försök igen när du har nät.'
                : 'Svaret gick inte att spara just nu. Försök igen om en stund.',
            )
          }
        })(event)
      }}
    >
      <fieldset className="form__field">
        <legend>Kommer ni på matchen?</legend>
        {STATUSES.map(({ value, label }) => (
          <label key={value} className="form__choice">
            <input type="radio" value={value} {...register('status')} />
            {label}
          </label>
        ))}
      </fieldset>

      {showCount && (
        <div className="form__field">
          <label htmlFor="antal">Hur många kommer?</label>
          {/* valueAsNumber: en <select> ger sträng, och schemat vill ha ett tal. */}
          <select
            id="antal"
            aria-describedby={errors.count ? 'antal-fel' : undefined}
            aria-invalid={errors.count ? true : undefined}
            {...register('count', { valueAsNumber: true })}
          >
            {Array.from({ length: MAX_ATTENDANCE_COUNT + 1 }, (_, index) => index).map((count) => (
              <option key={count} value={count}>
                {count}
              </option>
            ))}
          </select>
          {errors.count && (
            <p className="form__error" id="antal-fel">
              {errors.count.message}
            </p>
          )}
        </div>
      )}

      {failure !== null && (
        <p className="state state--error" role="alert">
          {failure}
        </p>
      )}

      <div className="actions">
        <button type="submit" className="button" disabled={isSubmitting}>
          {isSubmitting ? 'Sparar…' : current === null ? 'Svara' : 'Ändra svar'}
        </button>
      </div>
    </form>
  )
}
