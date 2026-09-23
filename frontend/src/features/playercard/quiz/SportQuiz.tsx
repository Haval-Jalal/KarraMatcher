import { useEffect, useRef, useState } from 'react'

import { QUESTIONS, QUIZ_LENGTH, type QuizQuestion } from './questions'
import { readBestScore, recordScore } from './quizStore'

/**
 * Sportquizet i spelarkortet (`#206`).
 *
 * <h3>En kul grej, inte en tävling som räknas</h3>
 *
 * Quizet är något förälder och barn gör tillsammans, i samma anda som spelarkortet. Det
 * som händer här stannar på telefonen (§KM.2): frågorna följer med appen, och svaret man
 * klickar på når aldrig en server. Rekordet sparas lokalt, skilt från kortet.
 *
 * <h3>Tillstånden</h3>
 *
 * Vila → en fråga i taget med direkt besked om rätt eller fel → resultatet med "spela
 * igen". Beskedet står i text (<c>role="status"</c>), inte bara i en färg, och fokus
 * flyttas till varje ny fråga och till resultatet så att den som använder tangentbord
 * eller skärmläsare följer med (§KM.0 A3).
 */

type Phase =
  | { name: 'idle' }
  | {
      name: 'playing'
      questions: QuizQuestion[]
      index: number
      score: number
      picked: number | null
    }
  | { name: 'done'; score: number }

/** Slumpar fram en omgång ur banken. En kopia sorteras slumpvis — banken rörs inte. */
function pickQuestions(): QuizQuestion[] {
  return [...QUESTIONS]
    .map((question) => ({ question, sort: Math.random() }))
    .sort((a, b) => a.sort - b.sort)
    .map((entry) => entry.question)
    .slice(0, QUIZ_LENGTH)
}

export function SportQuiz() {
  const [phase, setPhase] = useState<Phase>({ name: 'idle' })
  const [best, setBest] = useState<number>(() => readBestScore())
  const focusRef = useRef<HTMLHeadingElement>(null)

  // Fokus flyttas till varje ny fråga och till resultatet. Nyckeln ändras när det finns en
  // ny rubrik att landa på, inte medan man svarar på samma fråga.
  const focusKey =
    phase.name === 'playing' ? `q-${String(phase.index)}` : phase.name === 'done' ? 'done' : 'idle'

  useEffect(() => {
    if (phase.name !== 'idle') {
      focusRef.current?.focus()
    }
  }, [focusKey, phase.name])

  if (phase.name === 'idle') {
    return (
      <div className="quiz">
        <p className="quiz__intro">
          {`Testa era sportkunskaper tillsammans — ${String(QUIZ_LENGTH)} frågor om fotboll och idrott.`}
        </p>

        {best > 0 && (
          <p className="quiz__record">{`Bästa resultat på den här telefonen: ${String(best)} av ${String(QUIZ_LENGTH)}.`}</p>
        )}

        <button
          type="button"
          className="button"
          onClick={() => {
            setPhase({
              name: 'playing',
              questions: pickQuestions(),
              index: 0,
              score: 0,
              picked: null,
            })
          }}
        >
          Starta quizet
        </button>
      </div>
    )
  }

  if (phase.name === 'done') {
    return (
      <div className="quiz">
        <h3 className="quiz__result" ref={focusRef} tabIndex={-1}>
          {`${String(phase.score)} av ${String(QUIZ_LENGTH)} rätt!`}
        </h3>

        <p className="quiz__record">
          {phase.score >= best && phase.score > 0
            ? 'Nytt rekord på den här telefonen!'
            : `Bästa resultat på den här telefonen: ${String(best)} av ${String(QUIZ_LENGTH)}.`}
        </p>

        <button
          type="button"
          className="button"
          onClick={() => {
            setPhase({
              name: 'playing',
              questions: pickQuestions(),
              index: 0,
              score: 0,
              picked: null,
            })
          }}
        >
          Spela igen
        </button>
      </div>
    )
  }

  const question = phase.questions[phase.index]

  // Index hålls alltid inom omgången, men typen vet inte det. En vakt är billigare än ett
  // antagande som blir en krasch den dag flödet ändras.
  if (question === undefined) {
    return null
  }

  const answered = phase.picked !== null
  const questionNumber = phase.index + 1

  const answer = (choice: number) => {
    if (phase.picked !== null) {
      return
    }

    const correct = choice === question.answerIndex

    setPhase({ ...phase, picked: choice, score: phase.score + (correct ? 1 : 0) })
  }

  const next = () => {
    if (phase.index + 1 < phase.questions.length) {
      setPhase({ ...phase, index: phase.index + 1, picked: null })

      return
    }

    setBest(recordScore(phase.score))
    setPhase({ name: 'done', score: phase.score })
  }

  return (
    <div className="quiz">
      <p className="quiz__progress">{`Fråga ${String(questionNumber)} av ${String(QUIZ_LENGTH)}`}</p>

      <h3 className="quiz__question" id="quiz-fraga" ref={focusRef} tabIndex={-1}>
        {question.question}
      </h3>

      <div className="quiz__options" role="group" aria-labelledby="quiz-fraga">
        {question.options.map((option, choice) => (
          <button
            key={option}
            type="button"
            className={optionClass(choice, phase.picked, question.answerIndex)}
            // Efter svaret är knapparna bara ett facit; att låta dem tas om vore att låta
            // barnet ändra sitt svar när det redan sett rätt.
            disabled={answered}
            onClick={() => {
              answer(choice)
            }}
          >
            {option}
          </button>
        ))}
      </div>

      {answered && (
        <p className="quiz__feedback" role="status">
          {phase.picked === question.answerIndex
            ? 'Rätt!'
            : `Inte riktigt — rätt svar är ${question.options[question.answerIndex]}.`}
        </p>
      )}

      {answered && (
        <button type="button" className="button" onClick={next}>
          {phase.index + 1 < phase.questions.length ? 'Nästa fråga' : 'Se resultatet'}
        </button>
      )}
    </div>
  )
}

/**
 * Klassen på ett svarsalternativ.
 *
 * <para>
 * Innan svaret är alla neutrala. Efter svaret markeras det rätta alltid — och det felval
 * man råkade göra. Färgen är aldrig ensam bärare: texten "Rätt!"/"Inte riktigt" och
 * markeringen av rätt svar säger samma sak utan färgseende (§KM.0 A3).
 * </para>
 */
function optionClass(choice: number, picked: number | null, answerIndex: number): string {
  const base = 'button quiz__option'

  if (picked === null) {
    return base
  }

  if (choice === answerIndex) {
    return `${base} quiz__option--correct`
  }

  if (choice === picked) {
    return `${base} quiz__option--wrong`
  }

  return base
}
