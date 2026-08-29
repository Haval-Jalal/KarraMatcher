# Byggkontexten är repo-roten, inte backend/. Det är för att global.json och
# .editorconfig ligger där — containern ska bygga med samma SDK och samma
# analysregler som CI och utvecklarmaskinen.
#
#   docker build -f backend/Dockerfile -t karramatcher-api .

# ---------- Bygg ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Projektfilerna först, separat från källkoden: då kan Docker återanvända
# restore-lagret så länge inga beroenden ändrats.
COPY global.json .editorconfig ./
COPY backend/Directory.Build.props backend/
COPY backend/src/KarraMatcher.Domain/KarraMatcher.Domain.csproj backend/src/KarraMatcher.Domain/
COPY backend/src/KarraMatcher.Application/KarraMatcher.Application.csproj backend/src/KarraMatcher.Application/
COPY backend/src/KarraMatcher.Infrastructure/KarraMatcher.Infrastructure.csproj backend/src/KarraMatcher.Infrastructure/
COPY backend/src/KarraMatcher.Api/KarraMatcher.Api.csproj backend/src/KarraMatcher.Api/
RUN dotnet restore backend/src/KarraMatcher.Api/KarraMatcher.Api.csproj

COPY backend/src/ backend/src/
RUN dotnet publish backend/src/KarraMatcher.Api/KarraMatcher.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ---------- Kör ----------
# Debian-baserad image med avsikt, inte alpine eller chiseled: appen behöver både
# ICU och tidszonsdata för Europe/Stockholm (§KM.5). En bantad image utan dem
# skulle få SwedishTime att kasta vid uppstart.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0

EXPOSE 8080

# APP_UID finns i .NET-imagen sedan version 8 och pekar på en icke-privilegierad
# användare. Sätts efter COPY, så att kopieringen inte behöver skrivrättigheter
# som den användaren saknar.
USER $APP_UID

ENTRYPOINT ["dotnet", "KarraMatcher.Api.dll"]
