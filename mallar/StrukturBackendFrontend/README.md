# Projektmall — Backend + Frontend (för framtida projekt)

Den här mappen är en **startmall** för nya fullstack-projekt (C# / .NET-backend + React/TypeScript-frontend).
Kopiera in den i ett nytt repo och fyll i platshållarna (`[...]`) så har du regelverk, spec-process
och standarder på plats från dag 1.

## Vad finns här

| Fil | Roll | Beskriver |
|-----|------|-----------|
| [`CLAUDE.md`](./CLAUDE.md) | **Regelverket (HUR)** — agenten följer detta i all kod | Process, arkitektur, säkerhet, DoD — obligatoriskt från start |
| [`STANDARDER-VID-BEHOV.md`](./STANDARDER-VID-BEHOV.md) | Tillägg som införs **vid behov** | Pagination, caching, jobb, rate limiting, tracing m.m. — inte i förväg (YAGNI) |
| [`SPEC.md`](./SPEC.md) | **Produktspec (VAD & VARFÖR)** — fylls i per projekt | De 4 filtren + full produktspecifikation |
| [`SPEC-EXEMPEL.md`](./SPEC-EXEMPEL.md) | Ifyllt exempel på en spec | Referens när du fyller i `SPEC.md` |
| [`docs/PROJEKT-HANDOFF.md`](./docs/PROJEKT-HANDOFF.md) | **Levande status** — läses först varje session | Status, beslut, öppna frågor, nästa steg |
| `index.html` | Referensdokument för människor | Djupgående förklaringar och kodexempel |
| `spec-assets/` | Bilder till spec-processen | De 4 filtren m.m. |
| `.claude/` | Claude Code-inställningar för projektet | Maskinlokala val (`settings.local.json` checkas inte in) |

## Så startar du ett nytt projekt

1. **Kopiera mappens innehåll** till ditt nya repo (gärna både ett BE- och ett FE-repo — `CLAUDE.md` ska finnas i båda).
2. **Fyll i `SPEC.md`** — börja med **Steg 0 (de 4 filtren)**. Bygg inget förrän problemet passerat alla fyra. Använd `SPEC-EXEMPEL.md` som mall.
3. **Anpassa `CLAUDE.md`** — ersätt platshållare (board-länk, plan-dokument, projektnamn) och bekräfta teknikstacken.
4. **Fyll i `docs/PROJEKT-HANDOFF.md`** med projektnamn, länkar och första milstolpen.
5. **Skapa repo-hygien** enligt `CLAUDE.md` → "Repo-uppstart": `README.md` per repo, `.env.example`, `.editorconfig`, ESLint/Prettier-config.
6. **Lägg upp en GitHub Projects-board** med kolumnerna `Backlog → Ready → In Progress → In Review → Done` och länka den i handoff-filen.

## Tvånivåsystemet (globalt + projekt)

`CLAUDE.md` är **projektlagret**. Generella regler, hooks, agenter och minne ligger globalt i `~/.claude/`.
Vid konflikt har projektlagret företräde. Se avsnittet "Infrastruktur & Claude Code-konfiguration" i `CLAUDE.md`.

> Underhåll: hittar du en regel som saknas eller blivit fel under ett skarpt projekt — uppdatera den här mallen
> så nästa projekt får den med sig.
