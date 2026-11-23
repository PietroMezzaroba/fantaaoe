# syntax=docker/dockerfile:1
# Immagine del sito FantaAoE per il server (Linux ARM64).
# La compila GitHub Actions a ogni push su main: non serve Docker in locale.

# 1) Client Vue (cartella client/): se c'è package.json lo compila, altrimenti salta.
FROM --platform=$BUILDPLATFORM node:22-alpine AS client
WORKDIR /src
COPY client/ ./
RUN if [ -f package.json ]; then npm ci && npm run build; else mkdir -p dist; fi

# 2) Server ASP.NET Core (cartella server/), compilato per l'architettura di destinazione.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS server
ARG TARGETARCH
ARG PROGETTO=FantaAoe.Api.csproj
WORKDIR /src
COPY server/ ./
RUN dotnet publish "$PROGETTO" -c Release -a "$TARGETARCH" --self-contained false -o /out
# cartella vuota per le chiavi dei cookie di sessione (diventa un volume sul server)
RUN mkdir -p /vuota

# 3) Immagine finale: solo il runtime, utente non root, porta 8080.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
LABEL org.opencontainers.image.source=https://github.com/PietroMezzaroba/fantaaoe
ARG VERSIONE=locale
ENV VERSIONE=$VERSIONE
WORKDIR /app
COPY --from=server /out ./
# la build di Vue (dist/) finisce in wwwroot e sostituisce la pagina segnaposto
COPY --from=client /src/dist ./wwwroot/
COPY --from=server --chown=1654:1654 /vuota /keys
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "FantaAoe.Api.dll"]
