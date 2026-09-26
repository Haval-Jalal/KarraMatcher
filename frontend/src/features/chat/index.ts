// ChatPage/TeamChatPage exporteras medvetet INTE här. De laddas bara via `routes.tsx`
// dynamiska `import()`, och en barrel-återexport hade dragit in dem statiskt i entry-chunken
// igen (Rollups INEFFECTIVE_DYNAMIC_IMPORT) — då splittas de inte. Importera sidan direkt.
export { useMyTrupper } from './useChat'
export type { ChatMessage, MemberTrupp, ReportedMessage, ScheduledMessage } from './chatApi'
