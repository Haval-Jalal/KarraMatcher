# PROJEKT-HANDOFF — `[PROJEKTNAMN]` (telefonapp)

> **Detta är den första filen som läses i varje ny session** (av människa och agent).
> Den ska alltid spegla **faktisk** status. Uppdateras enligt §0 p.14 i [`CLAUDE.md`](../CLAUDE.md).
> Skriv ingen kod förrän ett issue är valt ur `Ready` och godkänt av människa.

---

## 🔎 Snabbstatus
- **Fas:** Discovery — Steg 0 klar (problem validerat), nästa: produktspec + MVP-scope
- **Senast uppdaterad:** `[ÅÅÅÅ-MM-DD]` av `[namn]`
- **Aktuell milstolpe:** `[t.ex. MVP — mål-datum ÅÅÅÅ-MM-DD]`
- **Hälsa:** `[🟢 på plan / 🟡 risk / 🔴 blockerad]`

## 🧱 Teknikstack (bekräftad)
- **App (FE):** React Native + Expo, TypeScript, Expo Router, TanStack Query, React Hook Form + Zod, expo-secure-store.
- **Backend (BE):** C# / .NET (LTS), EF Core, MediatR, FluentValidation.
- **Release:** EAS Build / EAS Update, App Store Connect (iOS) + Google Play Console (Android).
- **Plattformsmål:** 100 % funktion på både iOS och Android från gemensam kodbas.

## 🔗 Viktiga länkar
| Resurs | Länk |
|--------|------|
| Projektboard (GitHub Projects) | `[länk]` |
| Backend-repo | `[länk]` |
| App-repo (React Native) | `[länk]` |
| Produktspecifikation | [`SPEC.md`](../SPEC.md) |
| Plan / roadmap | `[t.ex. docs/MVP-PLAN.md]` |
| Apple Developer / App Store Connect | `[länk]` |
| Google Play Console | `[länk]` |
| Miljöer (staging/prod backend) | `[länkar]` |

## ✅ Klart hittills
*(Avklarade issues/milstolpar — senaste överst.)*
- `[#nr] [kort beskrivning]` — `[ÅÅÅÅ-MM-DD]`

## 🚧 Pågår nu
| Issue | Vem | Branch | Status |
|-------|-----|--------|--------|
| `[#nr]` | `[namn]` | `[feature/...]` | `[In Progress / In Review]` |

## ➡️ Nästa steg
*(Vad som tas härnäst ur `Ready`-kolumnen — i prioordning.)*
1. `[#nr] [kort beskrivning]`
2. `[...]`

## 🧭 Viktiga beslut (ADR-light)
| Datum | Beslut | Motivering | Konsekvens |
|-------|--------|------------|-----------|
| `2026-06-29` | Stack: React Native + Expo (app) + C#/.NET (backend) | Maximalt återbruk av befintlig React/TS-mall; en kodbas → iOS + Android; mogen, granskningsklar backend | FE-mönster återanvänds; web-specifika delar (DOM/Vite/localStorage) ersatta med mobil-motsvarigheter |
| `2026-07-01` | Produkt: tvåsidig marknadsplats hantverkare ↔ privatpersoner (offert-app) | SPEC.md Steg 0 genomförd — passerar 4 filtren med villkor | Bygger på hantverkarsidan (betalande, frekvent) |
| `2026-07-01` | Affärsmodell: hantverkare betalar abonnemang (~399 kr/mån), privatperson gratis | Förutsägbar MRR; undviker Offertas ogillade per-lead-modell | Offertverktyget måste ge dagligt värde i sig (cold-start på betalande sidan) |
| `2026-07-01` | Go-to-market: lokalt/nischat först | Marknadsplatser kräver lokal likviditet; löser hönan-och-ägget | Lansera region/bransch i taget, inte hela Sverige direkt |

## ❓ Öppna frågor
- `[Vad ska appen göra? — fyll i SPEC.md Steg 0 (4 filtren) innan bygge.]`
- `[Apple Developer-konto + Google Play-konto registrerade? (krävs för att kunna släppa.)]`

## ⚠️ Kända risker & blockerare
- `[Risk/blockerare — ägare — plan.]`
