# SPEC.md — Produktspecifikation: `[PROJEKTNAMN]`

> **Hur den här filen används:** Detta beskriver **VAD** som ska byggas och **VARFÖR**.
> `CLAUDE.md` beskriver **HUR** (arkitektur, standarder, säkerhet). Agenten läser båda.
> Fyll i allt inom `[hakparenteser]`. Lämna inget öppet utan att markera det som ett antagande.
>
> **Arbetsordning:** Gå igenom **Steg 0** först. Bygg inte förrän problemet passerat de fyra filtren —
> annars bygger ni något ingen kund betalar för. Därefter fylls resten av specen i, och agenten
> bygger feature för feature enligt `CLAUDE.md`.

---

# 🎯 STEG 0 — Validera problemet: De 4 filtren

> *Every problem worth building on passes four tests. Fail even one, and you have work to do.*
> Detta steg avgör om produkten kan **nå och behålla kund**. Svara ärligt — det är billigare att
> upptäcka ett svagt problem här än efter sex månaders kod.

![De 4 filtren – översikt](./spec-assets/filter-oversikt.png)

| # | Filter | Frågan | Ditt svar |
|---|--------|--------|-----------|
| 1 | **Painful** | Skulle någon *betala för att slippa* problemet idag? | `[ ]` |
| 2 | **Frequent** | Händer det dagligen/veckovis — inte en gång om året? | `[ ]` |
| 3 | **Large Enough** | Finns det tillräckligt många du faktiskt når? | `[ ]` |
| 4 | **Underserved** | Är dagens lösning dålig, dyr eller saknas? | `[ ]` |

---

## Filter 1 — Painful 🔥
**Test:** Skulle någon betala för att **INTE** ha det här problemet idag? Inte "gillar om det vore gratis" — öppna plånboken nu.

| 🔴 Hair on fire (bygger företag) | 🟢 Itchy (bygger bara feature requests) |
|----------------------------------|------------------------------------------|
| I kris. Använder vilken lösning som helst för att få stopp. Jämför inte alternativ. | Irriterande. De klagar, bygger workarounds, testar kanske en gratis trial. |

> **Sensei-not:** Fråga vad personen *gör idag* åt problemet. "Inget" = itchy. "Anlitat någon, byggt ett kalkylark, betalar en konsult, jonglerar fem dåliga verktyg" = hair on fire — de har redan bevisat att de betalar.

<details><summary>📷 Visa förklaringsbild</summary>

![Filter 1 – Painful](./spec-assets/filter-1-painful.png)
</details>

**▶ Ditt svar:** `[Beskriv smärtan. Vad gör målgruppen idag för att lösa den? Är de "hair on fire" eller "itchy"?]`

---

## Filter 2 — Frequent 🔁
**Test:** Händer det dagligen, veckovis — eller en gång om året?

| Frekvens | Bedömning |
|----------|-----------|
| En gång om året | Svårt att ta återkommande betalt. Kanske ett engångsverktyg. |
| En gång i månaden | Möjligt — men svag retention-signal. |
| Varje vecka | Solitt. Folk betalar månadsvis och stannar. |
| **Varje dag** | **Guld. Det är det här som får SaaS att fungera.** |

> **Viktigt:** Frekvens räcker inte ensamt. En daglig irritation som kostar 30 sekunder är fortfarande bara *itchy*. **Painful + Frequent** är där riktiga produkter lever.

<details><summary>📷 Visa förklaringsbild</summary>

![Filter 2 – Frequent](./spec-assets/filter-2-frequent.png)
</details>

**▶ Ditt svar:** `[Hur ofta uppstår problemet? Kombinera med Filter 1 — är det både smärtsamt OCH frekvent?]`

---

## Filter 3 — Large Enough 📈
**Test:** Hur många har problemet — och kan du nå dem?

> **1 000 true fans-formeln:** `1 000 kunder × 1 000 €/år = 1 000 000 € ARR` → ett riktigt företag.

| 🟢 En bra nisch | 🔴 För smal nisch |
|-----------------|-------------------|
| "Svenska småföretagare som kämpar med fakturering." Miljoner finns. | "Vänsterhänta designers i Göteborg på en specifik mjukvaruversion." Kanske 50 st. |

> **Sensei-not:** "Alla med en smartphone" är **ingen** marknad. "Svenska HR-chefer på företag med 50–200 anställda" är det. Var specifik — kontrollera sedan att de är tillräckligt många. *(Detta svar blir din "Målgrupp" i avsnitt 2.)*

<details><summary>📷 Visa förklaringsbild</summary>

![Filter 3 – Large Enough](./spec-assets/filter-3-large-enough.png)
</details>

**▶ Ditt svar:** `[Definiera den specifika målgruppen. Uppskatta antalet och hur du når dem. Räkna på 1000×pris.]`

---

## Filter 4 — Underserved 🎯
**Test:** Konkurrenter är inte ett dåligt tecken — de bevisar att marknaden finns. Den riktiga frågan: **varför gör DU det bättre?**

| Strukturell fördel | Innebörd |
|--------------------|----------|
| **Price** | 10× billigare — inte 10 % billigare. |
| **Distribution** | Du når kunder de inte kan nå. |
| **Technology** | Ny teknik (t.ex. AI) gör något som inte gick förut. |
| **Segment** | Du betjänar en kund de ignorerat. |
| **Simplicity** | Deras är överbyggt; ditt gör en sak perfekt. |

> **Viktigt:** "Bättre designat" och "fler features" är **inte** strategier — det är exekvering. **Strategi = strukturell fördel.** Inget tydligt svar ännu är okej i vecka 1. Du behöver ett innan vecka 2.

<details><summary>📷 Visa förklaringsbild</summary>

![Filter 4 – Underserved](./spec-assets/filter-4-underserved.png)
</details>

**▶ Ditt svar:** `[Vilken av de fem strukturella fördelarna är din? Varför vinner du strukturellt — inte bara "snyggare"?]`

---

## ✅ Filter-scorecard (grind innan bygge)

| Filter | Pass? | Kommentar |
|--------|:-----:|-----------|
| 1. Painful | `[J/N]` | `[...]` |
| 2. Frequent | `[J/N]` | `[...]` |
| 3. Large Enough | `[J/N]` | `[...]` |
| 4. Underserved | `[J/N]` | `[...]` |

> **Beslut:** Passerar alla fyra → fortsätt till specen nedan. Faller ett → `[beskriv vad som behöver utforskas/justeras innan bygge]`.

---

# 📋 PRODUKTSPECIFIKATION

## 1. Vision & mål
- **One-liner (hisspitch):** `[Vad är produkten i en mening?]`
- **Mål:** `[Vad ska uppnås — affärsmål och användarvärde?]`
- **Framgångsmått (KPI:er):** `[Hur mäts framgång? T.ex. aktiva användare, konvertering, retention.]`

## 2. Målgrupp & kund
*(Bygger på ditt svar i Filter 3 — var specifik.)*
- **Primär målgrupp:** `[Specifik persona, inte "alla".]`
- **Användarkontext:** `[När/var/hur används produkten?]`
- **Köpare vs användare:** `[Är den som betalar samma som den som använder?]`

## 3. Problem & lösning
- **Problem:** `[Beskriv smärtan — från Filter 1 & 2.]`
- **Dagens alternativ:** `[Vad gör de idag? Konkurrenter/workarounds.]`
- **Vår lösning:** `[Hur löser vi det — och varför bättre, från Filter 4?]`

## 4. Omfattning (scope)
- **MVP — ingår:** `[Minsta version som löser kärnproblemet.]`
- **Ingår INTE (nu):** `[Explicit utanför scope — viktigt mot scope creep.]`
- **Framtida (backlog):** `[Idéer för senare.]`

## 5. Features / användarberättelser
*Prioritera med MoSCoW. Format: "Som [roll] vill jag [mål] så att [värde]."*

| Prio | Feature / user story | Acceptanskriterier |
|------|----------------------|--------------------|
| Must | `[Som ... vill jag ...]` | `[Klart när ...]` |
| Must | `[...]` | `[...]` |
| Should | `[...]` | `[...]` |
| Could | `[...]` | `[...]` |
| Won't (nu) | `[...]` | — |

## 6. Domänmodell / entiteter
*(Underlag för agentens Domain-lager — se Clean Architecture i `CLAUDE.md`.)*
- **Entiteter & relationer:** `[T.ex. Order 1—* OrderItem, Customer 1—* Order]`
- **Viktiga affärsregler/invarianter:** `[T.ex. "En order kan inte bekräftas utan rader."]`
- **Nyckelfält per entitet:** `[Lista de centrala fälten.]`

## 7. Roller & behörigheter
*(Underlag för policy-baserad auktorisering i `CLAUDE.md`.)*

| Roll | Får göra | Får INTE göra |
|------|----------|---------------|
| `[Admin]` | `[...]` | `[...]` |
| `[User]` | `[...]` | `[...]` |
| `[Gäst]` | `[...]` | `[...]` |

## 8. API & integrationer
- **Externa tjänster:** `[Betalning, e-post, BankID, etc.]`
- **Inkommande integrationer:** `[Vem anropar oss?]`
- **Datautbyte/format:** `[REST/JSON, webhooks, etc.]`

## 9. UI — sidor & flöden
- **Huvudsidor/vyer:** `[Lista skärmar.]`
- **Kritiska användarflöden:** `[T.ex. registrering → skapa order → betala.]`
- **Designkrav:** `[Designsystem, varumärke, responsivitet.]`

## 10. Icke-funktionella krav
- **Prestanda:** `[Svarstider, samtidiga användare, datavolym.]`
- **Säkerhet:** `[Utöver baslinjen i CLAUDE.md — särskilda krav?]`
- **GDPR/PII:** `[Vilka personuppgifter? Laglig grund? Gallringsregler?]`
- **Tillgänglighet:** `[WCAG-nivå om upphandlingskrav.]`
- **Skalbarhet/drift:** `[Förväntad tillväxt, SLA, upptid.]`
- **Webbläsare/enheter:** `[Stöd som krävs.]`

## 11. Vilka "vid behov"-element gäller?
*(Markera vad som blir aktuellt — se [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md). Inför inte i förväg.)*

| Element | Aktuellt? | Trigger som uppfylls |
|---------|:---------:|----------------------|
| Pagination | `[J/N]` | `[...]` |
| Caching / Redis | `[J/N]` | `[...]` |
| Background jobs | `[J/N]` | `[...]` |
| Global state (frontend) | `[J/N]` | `[...]` |
| i18n (flerspråk) | `[J/N]` | `[...]` |
| Fältkryptering / CSRF / m.m. | `[J/N]` | `[...]` |

## 12. Definition of Done & acceptans
- **Per feature:** Se checklistan i `CLAUDE.md`.
- **Leveransklart när:** `[Övergripande acceptanskriterier för kund.]`

## 13. Antaganden & öppna frågor
- **Antaganden:** `[Vad utgår vi från tills annat bevisats?]`
- **Öppna frågor:** `[Vad behöver besvaras innan/under bygget?]`

## 14. Milstolpar & leverans
| Milstolpe | Innehåll | Mål-datum |
|-----------|----------|-----------|
| `[MVP]` | `[...]` | `[...]` |
| `[Beta]` | `[...]` | `[...]` |
| `[Lansering]` | `[...]` | `[...]` |

---

> **Till agenten:** Börja med en kort plan baserad på denna spec. Bygg sedan **en vertical slice i taget**
> (Must-features först) enligt `CLAUDE.md`, med tester per steg. Föreslå "vid behov"-element när
> deras trigger uppfylls — bygg inte in dem i förväg.
