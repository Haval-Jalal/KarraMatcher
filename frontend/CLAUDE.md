# CLAUDE.md — frontend

> **Regelverket ligger i roten:** [`../CLAUDE.md`](../CLAUDE.md). Läs det först — det gäller fullt ut här.
> Lägg **aldrig** motstridiga regler i den här filen (§0 p.15). Detta är bara en påminnelse om det
> som är lätt att glömma på frontend-sidan.

## Innan du skriver en rad kod
1. Läs [`../docs/PROJEKT-HANDOFF.md`](../docs/PROJEKT-HANDOFF.md).
2. Ta ett issue ur `Ready`, flytta det till `In Progress`.
3. **Verifiera att BE-endpointen finns** — rätt metod, URL och form (§0 p.13). Saknas den, bygg den först.
4. Skapa en branch. Aldrig direktpush till `main`.

## De sex misstagen som är lättast att göra här

1. **Spara JWT i `localStorage`.** Access-token lever i minnet, refresh-token i `httpOnly`-cookie. Aldrig annat.
2. **Skicka spelarkortet någonstans.** Det lever i enhetens egen lagring och får aldrig hamna i ett
   API-anrop, en push-payload eller en felrapport (§KM.2). Service workern cachar aldrig auth-svar (§KM.8).
3. **Glömma offline-tillståndet.** Varje vy hanterar loading, error, empty, data **och offline**.
   Appen används på fotbollsplaner med dålig täckning.
4. **Blanda språk.** UI-text på svenska, kod och commits på engelska (§KM.9).
5. **Tappa tillgängligheten.** WCAG 2.1 AA är ett eget krav här — tangentbord, kontrast, märkta
   kontroller, synlig fokusmarkering, `prefers-reduced-motion`. Mor- och farföräldrar är riktiga användare.
6. **Lita på FE-kontroller för behörighet.** Att dölja en knapp är inte säkerhet. Kallelsen styrs av en
   serverside-flagga (§KM.7); FE speglar bara vad API:t säger.

## Före commit

```bash
npm run lint        # ESLint + Prettier, --max-warnings 0
npm run typecheck   # typkoll (tsc -b). INTE npx tsc --noEmit - se nedan
npm run test        # Vitest
```

> **`npx tsc --noEmit` typkollar ingenting här.** `tsconfig.json` är en lösningsfil
> (`"files": []` plus referenser till `tsconfig.app.json` och `tsconfig.node.json`), så
> kommandot har inga filer att kontrollera och avslutas med 0 även vid uppenbara typfel.
> Använd `npm run typecheck`, som kör `tsc -b` och följer referenserna. CI gör redan det.

## Mappstruktur

```
frontend/
  public/            manifest.webmanifest, ikoner
  src/
    app/             router, layouter, providers
    features/        matches/ teams/ players/ stats/ carpool/ attendance/ admin/ auth/
    components/      delade presentationskomponenter
    hooks/           delade hooks
    lib/             api-klient, datum och tidszon, ics, push, storage
    styles/
```

## Designarv

Appen ärver identiteten från den handbyggda föregångaren: lagfärgen som tema
(Gul `#D9A21B`, Blå `#1E3F8A`, Vit `#D9D9D9`, Svart `#161616`), Barlow och Barlow Condensed,
kortbaserad layout, stora träffytor. Mobil först, ljust och mörkt läge.
