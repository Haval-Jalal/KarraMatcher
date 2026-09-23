import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { writeCard } from '@/features/playercard'
import { emptyCard, type Child } from '@/features/playercard/storage/schema'
import { stubApi } from '@/test/apiStub'
import { renderRoute } from '@/test/renderRoute'

import { QUESTIONS, QUIZ_LENGTH } from '../questions'

// Källfilerna läses som text för kopplingskontrollen (§KM.2), inte för beteendet.
import QUIZ_SOURCE from '../SportQuiz.tsx?raw'
import QUESTIONS_SOURCE from '../questions.ts?raw'

/**
 * Sportquizet i spelarkortet (`#206`, §KM.2).
 *
 * <para>
 * En kul grej mellan förälder och barn. Testerna vaktar att den fungerar (spela igenom,
 * få rätt/fel-besked, se resultatet), att rekordet överlever en omladdning, och — det
 * viktigaste — att ingenting av det lämnar telefonen.
 * </para>
 */

function child(id: string, name: string): Child {
  return { id, name, shirtNumber: null, teamSlug: null, seenBadges: [] }
}

function openQuiz() {
  writeCard({ ...emptyCard(), children: [child('1', 'Elias')] })
  stubApi({})

  return renderRoute('/spelarkort/1')
}

/** Den fråga som visas just nu, läst ur rubriken quizet ger den. */
function currentQuestion(): string {
  return document.getElementById('quiz-fraga')?.textContent ?? ''
}

function correctAnswerFor(question: string): string {
  const match = QUESTIONS.find((candidate) => candidate.question === question)

  if (match === undefined) {
    throw new Error(`Okänd fråga: ${question}`)
  }

  return match.options[match.answerIndex]
}

function wrongAnswerFor(question: string): string {
  const match = QUESTIONS.find((candidate) => candidate.question === question)

  if (match === undefined) {
    throw new Error(`Okänd fråga: ${question}`)
  }

  const wrong = match.options.find((_, index) => index !== match.answerIndex)

  if (wrong === undefined) {
    throw new Error(`Frågan saknar felaktigt alternativ: ${question}`)
  }

  return wrong
}

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ingenting lämnar telefonen', () => {
  it('quizet importerar inte API-lagret', () => {
    // §KM.2. Vägen till ett nätverksanrop finns inte i quizets filer.
    for (const source of [QUIZ_SOURCE, QUESTIONS_SOURCE]) {
      expect(source).not.toContain('@/lib/api')
      expect(source).not.toContain('fetch(')
    }
  })

  it('rör inte nätet när man spelar', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)

    const user = userEvent.setup()
    openQuiz()

    await user.click(await screen.findByRole('button', { name: 'Starta quizet' }))
    await user.click(screen.getByRole('button', { name: correctAnswerFor(currentQuestion()) }))

    expect(fetchMock).not.toHaveBeenCalled()
  })
})

describe('att spela', () => {
  it('ger ett rätt-besked och tar en hela vägen till resultatet', async () => {
    const user = userEvent.setup()
    openQuiz()

    await user.click(await screen.findByRole('button', { name: 'Starta quizet' }))

    for (let step = 0; step < QUIZ_LENGTH; step += 1) {
      await user.click(screen.getByRole('button', { name: correctAnswerFor(currentQuestion()) }))

      expect(screen.getByRole('status')).toHaveTextContent('Rätt!')

      const advance = step < QUIZ_LENGTH - 1 ? 'Nästa fråga' : 'Se resultatet'
      await user.click(screen.getByRole('button', { name: advance }))
    }

    expect(
      screen.getByRole('heading', {
        name: `${String(QUIZ_LENGTH)} av ${String(QUIZ_LENGTH)} rätt!`,
      }),
    ).toBeInTheDocument()
  })

  it('säger vad rätt svar var när man svarar fel', async () => {
    const user = userEvent.setup()
    openQuiz()

    await user.click(await screen.findByRole('button', { name: 'Starta quizet' }))

    const question = currentQuestion()
    await user.click(screen.getByRole('button', { name: wrongAnswerFor(question) }))

    expect(screen.getByRole('status')).toHaveTextContent(
      `Inte riktigt — rätt svar är ${correctAnswerFor(question)}.`,
    )
  })

  it('visar frågans nummer', async () => {
    const user = userEvent.setup()
    openQuiz()

    await user.click(await screen.findByRole('button', { name: 'Starta quizet' }))

    expect(screen.getByText(`Fråga 1 av ${String(QUIZ_LENGTH)}`)).toBeInTheDocument()
  })
})

describe('rekordet', () => {
  it('överlever en omladdning av sidan', async () => {
    const user = userEvent.setup()
    const view = openQuiz()

    await user.click(await screen.findByRole('button', { name: 'Starta quizet' }))

    for (let step = 0; step < QUIZ_LENGTH; step += 1) {
      await user.click(screen.getByRole('button', { name: correctAnswerFor(currentQuestion()) }))
      const advance = step < QUIZ_LENGTH - 1 ? 'Nästa fråga' : 'Se resultatet'
      await user.click(screen.getByRole('button', { name: advance }))
    }

    expect(screen.getByText('Nytt rekord på den här telefonen!')).toBeInTheDocument()

    // Ny sidladdning: rekordet ska stå kvar på startskärmen.
    view.unmount()
    openQuiz()

    expect(
      await screen.findByText(
        `Bästa resultat på den här telefonen: ${String(QUIZ_LENGTH)} av ${String(QUIZ_LENGTH)}.`,
      ),
    ).toBeInTheDocument()
  })
})
