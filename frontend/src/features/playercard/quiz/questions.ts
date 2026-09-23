/**
 * Frågebanken till sportquizet (`#206`).
 *
 * <h3>Ingenting av det här lämnar telefonen</h3>
 *
 * Quizet är en kul grej mellan förälder och barn, i samma anda som spelarkortet (§KM.2).
 * Frågorna är statiska och följer med appen — inget hämtas, och svaret man klickar på når
 * aldrig en server. Modulen importerar med flit inte API-lagret; vägen ut finns inte här.
 *
 * <h3>Ton och innehåll</h3>
 *
 * Frågorna är för barn: enkla, sportrelaterade och utan luriga formuleringar. Rätt svar
 * ligger på olika platser i listan med flit, så att ingen kan gissa rätt på mönstret i
 * stället för på frågan.
 */

/** En fråga med tre svarsalternativ. */
export interface QuizQuestion {
  id: string
  question: string
  options: [string, string, string]
  /** Index i <see cref="options"/> för det rätta svaret. */
  answerIndex: 0 | 1 | 2
}

/** Hur många frågor en omgång innehåller. */
export const QUIZ_LENGTH = 5

/**
 * Frågorna. Fotboll i botten eftersom det är sporten appen handlar om, med några allmänna
 * idrottsfrågor för omväxling.
 */
export const QUESTIONS: readonly QuizQuestion[] = [
  {
    id: 'spelare-pa-plan',
    question: 'Hur många spelare har ett fotbollslag på planen samtidigt?',
    options: ['9', '11', '13'],
    answerIndex: 1,
  },
  {
    id: 'hattrick',
    question: 'Vad kallas det när en spelare gör tre mål i samma match?',
    options: ['Hattrick', 'Trippel', 'Trestjärna'],
    answerIndex: 0,
  },
  {
    id: 'rott-kort',
    question: 'Vilken färg har kortet domaren visar när någon blir utvisad?',
    options: ['Gult', 'Blått', 'Rött'],
    answerIndex: 2,
  },
  {
    id: 'malvakt-hander',
    question: 'Vem i laget får använda händerna inne på planen?',
    options: ['Målvakten', 'Kaptenen', 'Alla'],
    answerIndex: 0,
  },
  {
    id: 'halvlekar',
    question: 'Hur många halvlekar spelas en fotbollsmatch?',
    options: ['2', '3', '4'],
    answerIndex: 0,
  },
  {
    id: 'horna',
    question: 'Vad kallas sparken från hörnflaggan?',
    options: ['Frispark', 'Hörna', 'Straff'],
    answerIndex: 1,
  },
  {
    id: 'inkast',
    question: 'Vad gör man när bollen har gått ut på sidlinjen?',
    options: ['Nickar in den', 'Sparkar in den', 'Kastar in den'],
    answerIndex: 2,
  },
  {
    id: 'nick',
    question: 'Vilken kroppsdel nickar man bollen med?',
    options: ['Foten', 'Huvudet', 'Knäet'],
    answerIndex: 1,
  },
  {
    id: 'oavgjort',
    question: 'Vad kallas det när en match slutar lika?',
    options: ['Oavgjort', 'Övertid', 'Omspel'],
    answerIndex: 0,
  },
  {
    id: 'olympiska-ringar',
    question: 'Hur många ringar finns i den olympiska symbolen?',
    options: ['3', '5', '7'],
    answerIndex: 1,
  },
  {
    id: 'ishockey',
    question: 'Vilken sport spelas på is med klubba och en puck?',
    options: ['Bandy', 'Curling', 'Ishockey'],
    answerIndex: 2,
  },
  {
    id: 'basket-korg',
    question: 'Vad gör man för att göra poäng i basket?',
    options: ['Får bollen i korgen', 'Sparkar i mål', 'Slår bollen över nätet'],
    answerIndex: 0,
  },
]
