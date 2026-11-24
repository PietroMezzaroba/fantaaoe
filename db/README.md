# Database locale per sviluppo

Per lavorare sul backend ASP.NET Core in locale senza toccare il server:

## 1. Crea il database PostgreSQL
```bash
createdb fantaaoe
psql -d fantaaoe -f db/setup-locale.sql
```
Lo script crea:
- lo schema `fanta` (tabelle equivalenti alle viste di produzione dei TG Dito + 6 giocatori e 2 partite di esempio);
- lo schema `fanta_torneo` (identico a produzione: `utenti_discord`, `squadre`, `iscritti`, `disponibilita`, `ticket`, `asta_stato`, `asta_offerte`, `torneo_incontri`).

## 2. Avvia il backend .NET puntando al DB locale
Imposta la variabile d'ambiente `ConnectionStrings__Aoe` (o mettila in `server/appsettings.Development.json`, che non va committato):
```bash
export ConnectionStrings__Aoe="Host=localhost;Database=fantaaoe;Username=postgres;Password=LA_TUA_PASSWORD;Search Path=fanta,fanta_torneo"
cd server
dotnet run
```

## 3. Avvia il frontend Vue puntando al tuo backend locale
Di default `npm run dev` punta al sito online. Per puntare al tuo `dotnet run` locale passa `API_URL`:
```bash
cd client
npm install
API_URL=http://localhost:5000 npm run dev
```
*(Su PowerShell Windows: `$env:API_URL="http://localhost:5000"; npm run dev` — sostituisci `5000` con la porta su cui ascolta il tuo `dotnet run`.)*
