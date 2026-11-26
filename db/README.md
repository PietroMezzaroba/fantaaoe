# Database locale per sviluppo

> **Produzione:** le modifiche allo schema del server stanno in `db/migrazioni/`, un file per rilascio, da lanciare *prima* di pubblicare il codice che le usa. Ricordati i `GRANT` per l'utente del sito (`fanta_lettura`): sulle tabelle nuove non li riceve da solo.

Per lavorare sul backend ASP.NET Core in locale senza toccare il server:

## 1. Crea il database PostgreSQL
```bash
createdb fantaaoe
psql -d fantaaoe -f db/setup-locale.sql
```
Lo script crea:
- lo schema `fanta` (tabelle equivalenti alle viste di produzione dei TG Dito + 6 giocatori e 2 partite di esempio);
- lo schema `fanta_torneo` (identico a produzione: `utenti_discord`, `squadre`, `utenti_iscritti`, `disponibilita`, `ticket`, `asta_portafoglio_utente`, `asta_ordine_capitani`, `asta_giocatori_acquistati`, `asta_giocatori_chiamati`, `asta_offerte`, `configurazioni`).

## 2. Avvia il backend .NET puntando al DB locale
Imposta la variabile d'ambiente `ConnectionStrings__Aoe` (o mettila in `server/appsettings.Development.json`, che non va committato):
```bash
export ConnectionStrings__Aoe="Host=localhost;Database=fantaaoe;Username=postgres;Password=LA_TUA_PASSWORD;Search Path=fanta,fanta_torneo"
cd server
dotnet run
```

## 3. Avvia il frontend Vue puntando al tuo backend locale
Di default `npm run dev` punta al backend locale su `http://localhost:59173` (profilo `FantaAoe.Api`). Se il server non è avviato il client mostra la pagina "Server non raggiungibile" e riprova da solo. Per usare un altro backend (es. il sito online) passa `API_URL`:
```bash
cd client
npm install
API_URL=https://fantaaoe.tail17199a.ts.net npm run dev
```
*(Su PowerShell Windows: `$env:API_URL="https://fantaaoe.tail17199a.ts.net"; npm run dev`.)*
