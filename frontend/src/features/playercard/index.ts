export { BackupSection } from './BackupSection'
// PlayerCardPage/ChildrenPage exporteras inte via barreln — de laddas bara dynamiskt i
// `routes.tsx`. En barrel-återexport hade dragit in dem i entry-chunken. Se chat/index.ts.
export { possessive, seasonFor, summarise } from './season/season'
export type { Season, SeasonRow, TeamRecord } from './season/season'
export {
  BADGES,
  earnedBadges,
  isEarned,
  markEarnedAsSeen,
  totalsFor,
  unseenBadges,
} from './badges/badges'
export type { Badge, Totals } from './badges/badges'
export { BadgeCelebration } from './badges/BadgeCelebration'
export { BadgeList } from './badges/BadgeList'
export { decodeBackup, encodeBackup } from './backup/backupCode'
export { mergeCards } from './backup/mergeCards'
export { SportQuiz } from './quiz/SportQuiz'
export { MatchReportCard } from './MatchReportCard'
export { useMatchReports } from './useMatchReports'
export { usePlayerCard } from './usePlayerCard'
export { clearCard, readCard, writeCard } from './storage/playerCardStore'
export { InstallTip } from './storage/InstallTip'
export { StorageNotice } from './storage/StorageNotice'
export { requestPersistentStorage } from './storage/persistentStorage'
export { CURRENT_VERSION, emptyCard } from './storage/schema'
export type { Child, MatchReport, PlayerCardData } from './storage/schema'
