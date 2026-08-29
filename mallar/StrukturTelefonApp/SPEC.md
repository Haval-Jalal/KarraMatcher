# SPEC.md — Produktspecifikation: `[APPNAMN]`

> **Hur den här filen används:** Detta beskriver **VAD** som ska byggas och **VARFÖR**.
> `CLAUDE.md` beskriver **HUR** (arkitektur, standarder, säkerhet). Agenten läser båda.
> Fyll i allt inom `[hakparenteser]`. Lämna inget öppet utan att markera det som ett antagande.
>
> **Arbetsordning:** Gå igenom **Steg 0** först. Bygg inte förrän problemet passerat de fyra filtren —
> annars bygger ni en app ingen laddar ner och behåller.

---

# 🎯 STEG 0 — Validera problemet: De 4 filtren

> Detta steg avgör om appen kan **nå och behålla användare**. Svara ärligt — det är billigare att
> upptäcka ett svagt problem här än efter sex månaders kod.

| # | Filter | Frågan | Ditt svar |
|---|--------|--------|-----------|
| 1 | **Painful** | Skulle någon *betala för att slippa* problemet idag? | ✅ Ja (starkast för hantverkaren) |
| 2 | **Frequent** | Händer det dagligen/veckovis — inte en gång om året? | 🟡 Delvis (hög för hantverkare, låg för konsument) |
| 3 | **Large Enough** | Finns det tillräckligt många du faktiskt når? | ✅ Ja (rulla ut lokalt) |
| 4 | **Underserved** | Är dagens lösning dålig, dyr eller saknas? | 🟡 Villkorat (moat = likviditet, ej enkelhet) |

## Filter 1 — Painful 🔥
**Test:** Skulle någon betala för att **INTE** ha problemet idag? Inte "gillar om gratis" — öppna plånboken nu.
Fråga vad personen *gör idag* åt problemet. "Inget" = itchy. "Anlitat någon, byggt kalkylark, jonglerar fem dåliga appar" = hair on fire.

**▶ Ditt svar:** Smärtan finns på båda sidor men är **asymmetrisk**. **Hantverkaren (den betalande):** tappar jobb för att hen svarar för långsamt / hinner inte offerera — sitter på kvällen i Word eller tackar nej till förfrågningar. "Hair on fire" för de som aktivt jagar jobb. **Privatpersonen:** akut smärta att hitta en pålitlig hantverkare, men sällan (se Filter 2). → Vi bygger för hantverkarens smärta (offerera snabbt + få jobb); privatpersonens enkelhet är tratten som föder den. **Pass (J).**

## Filter 2 — Frequent 🔁
**Test:** Dagligen, veckovis — eller en gång om året? **Varje dag = guld** för en app (öppnas ofta = retention). En daglig irritation som tar 30 sekunder är dock fortfarande bara itchy. **Painful + Frequent** är där riktiga appar lever.

**▶ Ditt svar:** **Privatperson:** behöver en hantverkare ~1–3 ggr/år → låg frekvens, svag retention. Därför är konsumenten **tratten, inte intäkten**. **Hantverkaren:** ett dagligt offertverktyg + löpande jobbflöde → hög frekvens. Därför landar affären på **abonnemang på hantverkarsidan** (valt). **Villkor:** verktyget måste ge dagligt värde i sig så hantverkaren stannar och betalar även innan leadvolymen är hög. **Delvis pass (🟡) — löst genom att bygga på den frekventa, betalande sidan.**

## Filter 3 — Large Enough 📈
**Test:** Hur många har problemet — och kan du nå dem? *(1 000 kunder × 1 000 €/år = 1 000 000 € ARR.)*
"Alla med en smartphone" är **ingen** marknad. Var specifik och kontrollera att de är tillräckligt många.

**▶ Ditt svar:** Sverige har **hundratusentals hantverkare/hantverksföretag** och **miljoner husägare** — marknaden är stor nog i absoluta tal. **Begränsningen är lokal likviditet:** en matchning måste ske i samma region (rörmokare i Göteborg är värdelös för husägare i Kiruna). **Intäktsräkning:** 1 000 betalande hantverkare × 399 kr/mån ≈ **4,8 M kr ARR** — ett riktigt företag. Nåbar marknad **år 1 = en region eller nisch**, inte hela landet. **Pass (J), med lokal utrullning.**

## Filter 4 — Underserved 🎯
**Test:** Konkurrenter bevisar att marknaden finns. Frågan: **varför gör DU det bättre — strukturellt?**
Strukturell fördel = Price (10× billigare), Distribution (når kunder de inte når), Technology (ny teknik), Segment (ignorerad kund), Simplicity (gör en sak perfekt). "Snyggare app" är exekvering, inte strategi.

**▶ Ditt svar (svagast — kräver bevakning):** Marknaden är **trång** (Offerta.se, Servicefinder, Byggstart, Reco m.fl.). Vald fördel: **enklare för privatpersonen** (posta jobb med bild + kort text på ~30 sek). ⚠️ **Ärlig invändning:** konsument-enkelhet är (a) lätt att kopiera och (b) riktad mot den *lågfrekventa* sidan, medan den som betalar är *hantverkaren*. **Coherent tolkning:** enkelheten är **wedgen/tratten** som skapar jobbflöde; hantverkaren betalar för ett snabbt mobilt offertverktyg + en ström av lättbesvarade jobb. Den **varaktiga moaten är lokal likviditet (nätverkseffekt) + hantverkarlojalitet** — inte enkelheten i sig. **Villkorat pass — moaten måste bli likviditet, byggd nisch/region för nisch/region i taget.**

## ✅ Filter-scorecard (grind innan bygge)

| Filter | Pass? | Kommentar |
|--------|:-----:|-----------|
| 1. Painful | J | Stark för den betalande hantverkaren; svagare men OK för konsumenten |
| 2. Frequent | 🟡 | Låg för konsument, hög för hantverkare → abonnemang på hantverkarsidan |
| 3. Large Enough | J | Stor marknad; rulla ut **lokalt** (likviditet är regional) |
| 4. Underserved | 🟡 | Enkelhet = wedge; **varaktig moat = lokal likviditet + hantverkarlojalitet** |

> **Beslut:** Idén **passerar med två villkor** som måste hålla under hela bygget:
> 1. **Offertverktyget måste ge hantverkaren dagligt värde i sig** — innan leadvolymen är hög (löser cold-start på den betalande sidan).
> 2. **Go-to-market börjar lokalt/nischat** (en region eller bransch) för att nå likviditet innan breddning.
>
> Grönt att fortsätta till produktspecen. Filter 4:s moat (likviditet) och konsument-retention (Filter 2) ska bevakas som de största riskerna.

---

## 🧩 Strategisk sammanfattning (beslutad affärslogik)

- **Affärsmodell:** Hantverkare betalar **abonnemang** (~199–599 kr/mån, arbetsantagande 399 kr). Privatperson gratis.
- **Wedge (kortsiktig):** Enklaste sättet för privatpersonen att posta ett jobb (bild + text, 30 sek).
- **Moat (långsiktig):** Lokal likviditet (nätverkseffekt) + ett offertverktyg hantverkaren älskar och stannar i.
- **Cold-start-strategi:** Seeda hantverkarsidan lokalt först (verktyget har värde utan konsumenter), släpp sedan på konsumenterna i samma region.
- **Största risker att bevaka:** (1) konsument-retention, (2) att enkelheten kopieras innan likviditet byggts, (3) lokal cold-start.

---

# 📋 PRODUKTSPECIFIKATION

## 1. Vision & mål
- **One-liner (hisspitch):** `[Vad är appen i en mening?]`
- **Mål:** `[Affärsmål och användarvärde.]`
- **Framgångsmått (KPI:er):** `[T.ex. DAU/MAU, retention dag 1/7/30, konvertering, app-betyg.]`

## 2. Målgrupp & kund
- **Primär målgrupp:** `[Specifik persona, inte "alla".]`
- **Användarkontext:** `[När/var/hur används appen? På språng? Offline? En hand?]`
- **Köpare vs användare:** `[Är den som betalar samma som använder?]`

## 3. Problem & lösning
- **Problem:** `[Smärtan — från Filter 1 & 2.]`
- **Dagens alternativ:** `[Vad gör de idag? Konkurrerande appar/workarounds.]`
- **Vår lösning:** `[Hur löser vi det — och varför bättre, från Filter 4?]`

## 4. Omfattning (scope)
- **MVP — ingår:** `[Minsta version som löser kärnproblemet.]`
- **Ingår INTE (nu):** `[Explicit utanför scope.]`
- **Framtida (backlog):** `[Idéer för senare.]`

## 5. Features / användarberättelser
*Prioritera med MoSCoW. Format: "Som [roll] vill jag [mål] så att [värde]."*

| Prio | Feature / user story | Acceptanskriterier |
|------|----------------------|--------------------|
| Must | `[Som ... vill jag ...]` | `[Klart när ...]` |
| Should | `[...]` | `[...]` |
| Could | `[...]` | `[...]` |
| Won't (nu) | `[...]` | — |

## 6. Domänmodell / entiteter
*(Underlag för agentens Domain-lager — Clean Architecture i `CLAUDE.md`.)*
- **Entiteter & relationer:** `[T.ex. User 1—* Post]`
- **Viktiga affärsregler/invarianter:** `[...]`
- **Nyckelfält per entitet:** `[...]`

## 7. Roller & behörigheter
| Roll | Får göra | Får INTE göra |
|------|----------|---------------|
| `[Admin]` | `[...]` | `[...]` |
| `[Användare]` | `[...]` | `[...]` |
| `[Gäst]` | `[...]` | `[...]` |

## 8. API & integrationer
- **Externa tjänster:** `[Betalning (Stripe/in-app purchase), e-post, BankID, kartor, push (APNs/FCM), etc.]`
- **Datautbyte/format:** `[REST/JSON, webhooks.]`

## 9. App — skärmar & flöden
- **Huvudskärmar/vyer:** `[Lista skärmar — onboarding, login, hem, detalj, profil, inställningar...]`
- **Navigationsmönster:** `[Tabs? Stack? Drawer? Modaler?]`
- **Kritiska användarflöden:** `[T.ex. onboarding → registrering → kärnflöde → notis.]`
- **Designkrav:** `[Designsystem, varumärke, ljust/mörkt läge, plattformskonventioner iOS/Android.]`

## 10. Mobil-specifika krav
- **Plattformar & versioner:** `[Min iOS-version? Min Android-version? Telefon + surfplatta?]`
- **Offline:** `[Måste appen fungera offline? Helt, delvis, eller bara online?]`
- **Hårdvara/behörigheter:** `[Kamera, plats, notiser, biometrik, Bluetooth — vilka behövs och varför?]`
- **Push-notiser:** `[Vilka händelser triggar notis? Transaktionella, marknadsföring?]`
- **Deep linking:** `[Ska appen öppnas via länk/QR/push till specifik skärm?]`
- **Prestanda:** `[Förväntad startup-tid, liststorlekar, mediahantering.]`

## 11. Icke-funktionella krav
- **Säkerhet:** `[Utöver baslinjen — t.ex. certificate pinning, biometrik-tvång?]`
- **GDPR/PII:** `[Vilka personuppgifter? Laglig grund? Gallring? Kontoborttagning i appen?]`
- **Tillgänglighet:** `[VoiceOver/TalkBack-stöd, kontrast — WCAG-nivå om kundkrav.]`
- **Skalbarhet/drift (backend):** `[Förväntad tillväxt, SLA, upptid.]`

## 12. Vilka "vid behov"-element gäller?
*(Se [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md). Inför inte i förväg.)*

| Element | Aktuellt? | Trigger som uppfylls |
|---------|:---------:|----------------------|
| Pagination | `[J/N]` | `[...]` |
| Offline-först & lokal synk | `[J/N]` | `[...]` |
| Push-notis-tjänst (utbyggd) | `[J/N]` | `[...]` |
| Global state (Zustand/Context) | `[J/N]` | `[...]` |
| Native-funktioner (kamera/plats) | `[J/N]` | `[...]` |
| i18n (flerspråk) | `[J/N]` | `[...]` |
| Certificate pinning | `[J/N]` | `[...]` |

## 13. App store-krav (måste planeras tidigt)
- **Konton:** `[Apple Developer Program ($99/år) + Google Play Developer ($25 engång) registrerade?]`
- **Integritetspolicy & data:** `[Privacy labels (iOS) / Data Safety (Android) — vilken data samlas/delas?]`
- **Innehåll:** `[Åldersgräns, kontoborttagning i appen, in-app purchase-regler om digitalt innehåll säljs.]`

## 14. Antaganden & öppna frågor
- **Antaganden:** `[...]`
- **Öppna frågor:** `[...]`

## 15. Milstolpar & leverans
| Milstolpe | Innehåll | Mål-datum |
|-----------|----------|-----------|
| `[MVP / TestFlight + intern Android-test]` | `[...]` | `[...]` |
| `[Beta]` | `[...]` | `[...]` |
| `[Lansering i App Store + Play]` | `[...]` | `[...]` |

---

> **Till agenten:** Börja med en kort plan baserad på denna spec. Bygg sedan **en vertical slice i taget**
> (Must-features först) enligt `CLAUDE.md`, med tester per steg och verifiering på **både iOS och Android**.
