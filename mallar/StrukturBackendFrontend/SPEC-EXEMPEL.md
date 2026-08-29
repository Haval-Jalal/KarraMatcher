# SPEC-EXEMPEL.md — Ifyllt exempel: **Fakturo** (fakturaverktyg)

> **Detta är ett exempel** på hur en färdig `SPEC.md` kan se ut — använd det som referens
> när du fyller i din egen. Produkten är påhittad: ett enkelt faktureringsverktyg för svenska
> enmansföretagare/frilansare. Allt nedan är exempeltext, inte ett verkligt åtagande.

---

# 🎯 STEG 0 — Validera problemet: De 4 filtren

| # | Filter | Frågan | Svar |
|---|--------|--------|------|
| 1 | **Painful** | Skulle någon betala för att slippa problemet idag? | ✅ Ja |
| 2 | **Frequent** | Händer det dagligen/veckovis? | ✅ Veckovis |
| 3 | **Large Enough** | Tillräckligt många du når? | ✅ ~800 000 i SE |
| 4 | **Underserved** | Dålig/dyr/saknad lösning idag? | ✅ Ja (för dyrt/komplext) |

## Filter 1 — Painful 🔥
**▶ Svar:** Frilansare och enmansföretagare måste fakturera för att få betalt — men dagens verktyg är antingen dyra bokföringssviter (Fortnox, Visma) som är överdimensionerade, eller manuella Word/Excel-mallar som ser oprofessionella ut och saknar momsberäkning. Många **gör redan något åt det idag**: bygger egna Excel-mallar, betalar för en hel bokföringssvit de knappt använder, eller lägger timmar på manuellt arbete varje månad. Det är **hair on fire** — de spenderar redan tid/pengar.

## Filter 2 — Frequent 🔁
**▶ Svar:** En typisk frilansare skickar 2–10 fakturor i veckan och följer upp obetalda löpande. Det är **veckovis–dagligen**, inte en gång om året. Kombinerat med smärtan (Filter 1) → en produkt man betalar månadsvis för och stannar i.

## Filter 3 — Large Enough 📈
**▶ Svar:** Specifik målgrupp: **svenska enmansföretagare och frilansare (enskild firma / litet AB) som fakturerar tjänster, inte produkter.** I Sverige finns ~800 000 enskilda firmor. Vi behöver bara en bråkdel:
> `1 000 kunder × 1 200 kr/år = 1,2 MSEK ARR`. Når dem via SEO ("fakturamall", "fakturera utan företag"), frilans-communities och redovisningskonsulter.

## Filter 4 — Underserved 🎯
**▶ Svar:** Strukturell fördel = **Simplicity + Price**. De stora (Fortnox/Visma) är överbyggda bokföringssviter; vi gör **en sak perfekt** — skapa och skicka snygga fakturor på 60 sekunder — till en bråkdel av priset. Inte "snyggare UI" utan en strukturellt smalare produkt för ett segment de ignorerar (de som inte vill ha full bokföring).

## ✅ Filter-scorecard
| Filter | Pass? | Kommentar |
|--------|:-----:|-----------|
| 1. Painful | **J** | Betalar redan med tid/pengar (Excel, dyra sviter) |
| 2. Frequent | **J** | Veckovis fakturering + uppföljning |
| 3. Large Enough | **J** | ~800k enskilda firmor; behöver bara ~1 000 kunder |
| 4. Underserved | **J** | Simplicity + Price mot överbyggda konkurrenter |

> **Beslut:** Alla fyra passerar → fortsätt till specen.

---

# 📋 PRODUKTSPECIFIKATION

## 1. Vision & mål
- **One-liner:** Skapa, skicka och få betalt för proffsiga fakturor på under en minut — utan en hel bokföringssvit.
- **Mål:** Bli förstahandsvalet för svenska frilansare som vill fakturera enkelt. Affärsmål: 1 000 betalande kunder inom 18 månader.
- **Framgångsmått (KPI:er):** Aktiverade konton (skickat ≥1 faktura), månatlig retention >90 %, tid från registrering till första skickade faktura <10 min.

## 2. Målgrupp & kund
- **Primär målgrupp:** Svenska frilansare/enmansföretagare (enskild firma eller litet AB) som säljer tjänster och fakturerar 2–10 ggr/vecka.
- **Användarkontext:** Sitter ofta vid datorn efter avslutat uppdrag, vill snabbt skicka en faktura och gå vidare. Periodvis i mobilen.
- **Köpare = användare:** Ja, samma person.

## 3. Problem & lösning
- **Problem:** Fakturering är frekvent och måste se proffsig ut + ha rätt moms, men dagens alternativ är antingen för dyra/komplexa eller för manuella.
- **Dagens alternativ:** Fortnox/Visma (dyrt, överbyggt), Excel/Word-mallar (manuellt, oprofessionellt), eller gratis-generatorer (saknar uppföljning/historik).
- **Vår lösning:** Ett fokuserat verktyg: kundregister, fakturamall med automatisk moms- och summaberäkning, PDF + skicka via e-post, och statusuppföljning (skickad/betald/förfallen). Inget mer.

## 4. Omfattning (scope)
- **MVP — ingår:** Registrering/inloggning · skapa/redigera kund · skapa faktura (rader, moms, summa) · generera PDF · skicka via e-post · lista fakturor med status · markera som betald.
- **Ingår INTE (nu):** Full bokföring, SIE-export, integration mot Skatteverket, återkommande fakturor, flera användare per konto, betalningar inne i appen.
- **Framtida (backlog):** Påminnelser för förfallna fakturor, återkommande fakturor, Stripe/Swish-betalning, SIE-export, engelskt språkstöd.

## 5. Features / användarberättelser
| Prio | User story | Acceptanskriterier |
|------|-----------|--------------------|
| Must | Som frilansare vill jag registrera ett konto så att mina data sparas. | Konto skapas med e-post+lösenord; verifieringsmejl skickas. |
| Must | Som användare vill jag lägga till en kund så att jag kan fakturera den. | Kund med namn, org.nr, adress, e-post sparas och kan väljas. |
| Must | Som användare vill jag skapa en faktura med rader så att moms och totalsumma räknas ut automatiskt. | Rad (beskrivning, antal, à-pris, momssats) → netto, moms och brutto beräknas korrekt. |
| Must | Som användare vill jag generera en PDF och skicka den via e-post. | PDF skapas med mina uppgifter + kundens; mejl skickas; status blir "Skickad". |
| Must | Som användare vill jag se alla mina fakturor med status så att jag vet vad som är obetalt. | Lista visar nummer, kund, belopp, datum, status; filtrerbar. |
| Should | Som användare vill jag markera en faktura som betald. | Status ändras till "Betald" med betaldatum. |
| Should | Som användare vill jag se en faktura som "Förfallen" automatiskt efter förfallodatum. | Skickad + ej betald + förbi förfallodatum → status "Förfallen". |
| Could | Som användare vill jag ladda upp min logotyp så att fakturan blir varumärkt. | Logotyp visas på PDF. |
| Won't (nu) | Återkommande fakturor / betalning i appen. | — |

## 6. Domänmodell / entiteter
- **Entiteter & relationer:**
  - `User` 1—* `Customer`
  - `User` 1—* `Invoice`
  - `Customer` 1—* `Invoice`
  - `Invoice` 1—* `InvoiceLine`
- **Affärsregler/invarianter:**
  - En faktura måste ha minst en rad innan den kan skickas.
  - Fakturanummer är unikt och löpande per användare.
  - En skickad faktura kan inte redigeras (bara markeras betald/krediteras).
  - Moms beräknas per rad utifrån radens momssats (0/6/12/25 %).
- **Nyckelfält:**
  - `Invoice`: Id, Number, CustomerId, IssueDate, DueDate, Status (Draft/Sent/Paid/Overdue), Currency
  - `InvoiceLine`: Description, Quantity, UnitPrice, VatRate
  - `Customer`: Name, OrgNumber, Email, Address

## 7. Roller & behörigheter
| Roll | Får göra | Får INTE göra |
|------|----------|---------------|
| Användare (kontoägare) | Allt med sina egna kunder/fakturor (CRUD) | Se andras data |
| Admin (support, internt) | Läsa konton för support, spärra konton | Skapa/skicka fakturor i kundens namn |

> Auktorisering: varje query/command filtrerar på inloggad `UserId` (rad-nivå-ägarskap). Policy `Admin` för supportvyer.

## 8. API & integrationer
- **Externa tjänster:** E-postutskick (t.ex. SendGrid/Postmark) för fakturamejl + kontoverifiering. PDF-generering (server-side).
- **Inkommande integrationer:** Inga i MVP.
- **Datautbyte/format:** REST/JSON. `/api/v1/customers`, `/api/v1/invoices`. PDF som binär nedladdning.

## 9. UI — sidor & flöden
- **Huvudsidor:** Inloggning/registrering · Dashboard (fakturalista + status) · Kundlista · Skapa/redigera faktura · Inställningar (mina företagsuppgifter, logotyp).
- **Kritiska flöden:**
  1. Registrera → verifiera e-post → fyll i egna företagsuppgifter.
  2. Lägg till kund → skapa faktura → förhandsgranska PDF → skicka.
  3. Dashboard → markera faktura som betald.
- **Designkrav:** Rent, snabbt, mobilanpassat. Svensk lokalisering (SEK, datumformat, moms).

## 10. Icke-funktionella krav
- **Prestanda:** Fakturalista laddar <500 ms vid upp till 5 000 fakturor (kräver pagination). PDF-generering <3 s.
- **Säkerhet:** Baslinjen i `CLAUDE.md`. Rad-nivå-ägarskap (en användare når aldrig en annans data). Rate limiting på login.
- **GDPR/PII:** Personuppgifter = användarens och kundernas namn/adress/e-post/org.nr. Laglig grund: avtal. Gallring: konto + data raderas på begäran inom 30 dagar. PII loggas aldrig i klartext.
- **Tillgänglighet:** WCAG 2.1 AA som mål (semantisk HTML, tangentbord, kontrast).
- **Skalbarhet/drift:** Start på en instans; designat för att skala horisontellt. SLA-mål 99,5 % upptid.
- **Webbläsare/enheter:** Senaste Chrome/Edge/Safari/Firefox + mobil (responsivt).

## 11. Vilka "vid behov"-element gäller?
| Element | Aktuellt? | Trigger som uppfylls |
|---------|:---------:|----------------------|
| Pagination | **J** | Fakturalistan växer per användare → inför direkt för list-endpoints |
| Caching / Redis | N | En instans i start; ingen mätbar flaskhals än |
| Background jobs | **J** | E-postutskick + framtida förfallo-påminnelser → kör utanför request-tråden |
| Global state (frontend) | **J (lätt)** | Inloggad användare + företagsuppgifter delas → Context |
| i18n | N | Endast svenska i MVP (engelska i backlog) |
| Fältkryptering / CSRF | N | Bearer-token (ej cookie) → CSRF ej aktuellt; ingen särskilt känslig PII utöver standard |

## 12. Definition of Done & acceptans
- **Per feature:** Enligt checklistan i `CLAUDE.md`.
- **Leveransklart (MVP) när:** En användare kan registrera sig, lägga till en kund, skapa en faktura med korrekt moms, skicka den som PDF via e-post, se den i listan och markera som betald — allt med tester gröna och säkerhetsbaslinjen uppfylld.

## 13. Antaganden & öppna frågor
- **Antaganden:** Användarna har enskild firma/litet AB · momssatser 0/6/12/25 % räcker · betalning sker utanför appen (bankgiro/Swish manuellt) i MVP.
- **Öppna frågor:** Behövs kreditfaktura redan i MVP? · Vilken e-postleverantör? · Krav på fakturanummerserie från Skatteverket att ta hänsyn till?

## 14. Milstolpar & leverans
| Milstolpe | Innehåll | Mål-datum |
|-----------|----------|-----------|
| MVP | Konto, kund, faktura, PDF, skicka, lista, markera betald | `[v.1–6]` |
| Beta | Förfallo-status, logotyp, buggfixar med pilotkunder | `[v.7–10]` |
| Lansering | Påminnelser, betalningslänk (Swish/Stripe), publik release | `[v.11+]` |

---

> **Till agenten:** Detta är ett *exempel*. För ett skarpt projekt, läs den ifyllda `SPEC.md`,
> lägg en plan, och bygg en vertical slice i taget (Must-features först) enligt `CLAUDE.md`.
