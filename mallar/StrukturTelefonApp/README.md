# Infrastruktur telefonapp — startgrund (iOS + Android)

Den här mappen är **grunden/regelverket** för att bygga en mobilapp som fungerar till **100 % på både iOS och Android**
från en gemensam kodbas. Den är en mobil-anpassad vidareutveckling av webb-mallen `Struktur BackendFrontend`.

**Stack:** App = **React Native + Expo** (TypeScript). Backend = **C# / .NET**. Release = **EAS Build/Update** + App Store + Google Play.

## Vad finns här

| Fil | Roll | Beskriver |
|-----|------|-----------|
| [`CLAUDE.md`](./CLAUDE.md) | **Regelverket (HUR)** — agenten följer detta i all kod | Process, BE-arkitektur, app-arkitektur, säkerhet (inkl. på-enheten), build/release, DoD |
| [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md) | Tillägg **vid behov** | Pagination, offline-synk, push-tjänst, native-funktioner, cert pinning, MFA, DB-backup m.m. — inte i förväg (YAGNI) |
| [`SAKERHET-CHECKLISTA.md`](./SAKERHET-CHECKLISTA.md) | **Auditerbar säkerhets-checklista** — releasegrind | OWASP MASVS + ASVS/API Top 10 + App Store/Play — bockas av före release och vid kundgranskning |
| [`SPEC.md`](./SPEC.md) | **Produktspec (VAD & VARFÖR)** — fylls i per projekt | De 4 filtren + full produktspec, inkl. mobil-specifika krav och app store-krav |
| [`docs/PROJEKT-HANDOFF.md`](./docs/PROJEKT-HANDOFF.md) | **Levande status** — läses först varje session | Status, beslut, öppna frågor, nästa steg |
| `.claude/` | Claude Code-inställningar för projektet | Maskinlokala val (`settings.local.json` checkas inte in) |

## Vad som återanvänds från webb-mallen (och vad som är nytt)

| Område | Från webb-mallen | Anpassning för app |
|--------|------------------|--------------------|
| Process (§0) | ✅ Rakt över | — |
| Backend C#/.NET | ✅ ~95 % | + push (APNs/FCM), API-versionering för gamla appversioner, refresh-token-rotation |
| Frontend | ⚠️ Mönster behålls | React **web** (Vite/DOM/TanStack Router/localStorage) → **React Native + Expo** (Expo Router, native UI, expo-secure-store) |
| Spec-process | ✅ 100 % | + mobil-krav (offline, behörigheter, push) + app store-compliance |
| Säkerhetsbaslinje | ✅ 100 % | + säkerhet på enheten (Keychain/Keystore, biometrik, cert pinning) |
| Build/release | — | **Nytt:** EAS Build/Update, signering, App Store Connect + Play Console, privacy labels |

## Så startar du projektet

1. **Fyll i `SPEC.md`** — börja med **Steg 0 (de 4 filtren)**. Bygg inget förrän problemet passerat alla fyra.
2. **Anpassa `CLAUDE.md`** — ersätt platshållare (board-länk, appnamn) och bekräfta stacken.
3. **Fyll i `docs/PROJEKT-HANDOFF.md`** med appnamn, länkar och första milstolpen.
4. **Skapa två repon** (eller ett monorepo): `backend/` (.NET) och `app/` (React Native + Expo). `CLAUDE.md` ska finnas i båda.
5. **Repo-uppstart:** `README.md` per repo, `.env.example` (BE), `app.config.ts` + EAS-profiler (app), ESLint/Prettier/`.editorconfig`.
6. **Konton i tid:** Apple Developer Program + Google Play Developer (krävs för att kunna släppa — registrera tidigt, Apple-granskning tar tid).
7. **Projektboard** (GitHub Projects) med `Backlog → Ready → In Progress → In Review → Done`, länkad i handoff-filen.

## Nästa steg

Grunden är lagd. Härnäst: **fyll i `SPEC.md` Steg 0** tillsammans, så går vi vidare till **projektplaneringen**
(MVP-scope, skärmflöden, domänmodell och första milstolpen).

> Underhåll: hittar du en regel som saknas eller blivit fel under ett skarpt projekt — uppdatera den här grunden
> så nästa app-projekt får den med sig.
