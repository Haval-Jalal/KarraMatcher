import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'

import { App } from '@/app/App'
import { applyTheme, getStoredTheme } from '@/lib/theme'
// Snitten först: deklarationerna ska finnas när stilmallen börjar tillämpa dem.
import '@/styles/fonts.css'
import '@/styles/index.css'

// Inline-skriptet i index.html hann redan sätta temat; det här håller det rätt även om skriptet
// någon gång tas bort, och är en enda källa till hur valet tillämpas.
applyTheme(getStoredTheme())

const rootElement = document.getElementById('root')

if (!rootElement) {
  throw new Error('Hittade inte #root i index.html')
}

createRoot(rootElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
