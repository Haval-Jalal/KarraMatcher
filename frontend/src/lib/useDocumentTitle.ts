import { useEffect } from 'react'

const APP_NAME = 'Kärra Matcher'

/**
 * Sätter sidans titel.
 *
 * <p>
 * WCAG 2.4.2 är nivå A och kräver att varje sida har en titel som beskriver dess innehåll.
 * I en ensidesapp byts inte titeln av sig själv, så alla vyer hette "Kärra Matcher" —
 * vilket gör webbläsarens fliklista, historiken och bokmärkena obrukbara, och gör att en
 * skärmläsare säger samma sak vid varje sidbyte.
 * </p>
 */
export function useDocumentTitle(title: string): void {
  useEffect(() => {
    document.title = `${title} – ${APP_NAME}`
  }, [title])
}
