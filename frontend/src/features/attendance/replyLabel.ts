import type { AttendanceReply } from './attendanceApi'

/**
 * Svensk etikett för ett närvarosvar.
 *
 * Delad mellan admin-vyn (`AttendanceSection`) och tränar-vyn (`CoachKallelse`) så att "inget
 * svar" inte renderas olika beroende på var man står (`#614`). "Inget svar" framför ett ensamt
 * bindestreck: begripligare, och en skärmläsare läser annars upp "–" som ingenting eller "streck".
 */
export function replyLabel(reply: AttendanceReply | null): string {
  switch (reply) {
    case 'Coming':
      return 'Ja'
    case 'NotComing':
      return 'Nej'
    default:
      return 'Inget svar'
  }
}
