# CLAUDE.md — backend

> **Regelverket ligger i roten:** [`../CLAUDE.md`](../CLAUDE.md). Läs det först — det gäller fullt ut här.
> Lägg **aldrig** motstridiga regler i den här filen (§0 p.15). Detta är bara en påminnelse om det
> som är lätt att glömma på backend-sidan.

## Innan du skriver en rad kod
1. Läs [`../docs/PROJEKT-HANDOFF.md`](../docs/PROJEKT-HANDOFF.md).
2. Ta ett issue ur `Ready`, flytta det till `In Progress`.
3. Skapa en branch. Aldrig direktpush till `main`.

## De sex misstagen som är lättast att göra här

1. **Bygga en endpoint för barnstatistik.** Spelarkortet lever enbart på familjens enhet — det finns
   ingen tabell och ingen endpoint för det, och får inte införas (§KM.2). Ett arkitekturtest ska fälla
   bygget om någon gör det ändå.
2. **Exponera en entitet i API:t.** Allt in och ut är DTO-records. Aldrig EF-entiteter.
3. **Lägga en ny personuppgiftskolumn.** Endast förnamn och tröjnummer om ett barn (§KM.1). En ny
   PII-kolumn kräver ett beslut infört i handoff-filen i samma PR.
4. **Lagra lokal tid.** Allt i UTC (`timestamptz`), konvertering på ett enda ställe (§KM.5).
5. **Logga fel saker.** Aldrig barnnamn, e-post, push-endpoint, JWT eller användarfritext (§KM.10).
6. **Missa samåkningens regler.** Endast erbjudandets ägare svarar, platsräkningen sker server-side, och
   ett nekande utan meddelande ska avvisas (§KM.12).

## Före commit

```bash
dotnet build     # varningsfritt
dotnet test      # allt grönt
dotnet format    # inga diffar kvar
```

## Lösningsstruktur

```
backend/
  KarraMatcher.sln
  src/
    KarraMatcher.Domain/          inga ramverksberoenden alls
    KarraMatcher.Application/     CQRS-handlers, validatorer, interfaces
    KarraMatcher.Infrastructure/  EF Core, repositories, externa tjänster
    KarraMatcher.Api/             controllers, middleware, DI-uppsättning
  tests/
    KarraMatcher.Domain.Tests/
    KarraMatcher.Application.Tests/
    KarraMatcher.Architecture.Tests/    NetArchTest — beroenden pekar inåt
    KarraMatcher.Api.IntegrationTests/
```
