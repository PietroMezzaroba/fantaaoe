# FantaAoE

Sito del fantaAoE della community Age of Empires Italia.

**Indirizzo:** https://fantaaoe.tail17199a.ts.net

## Come si rilascia

Fai `git push` su `main`: GitHub Actions compila l'immagine per il server (Linux ARM64) e dopo qualche minuto il sito si aggiorna da solo. Docker in locale non serve. L'esito della compilazione si vede nella scheda **Actions** del repo.

## Struttura

| Cartella | Cosa |
|---|---|
| `server/` | API ASP.NET Core **.NET 10** (minimal API + Dapper + Npgsql), sola lettura sullo schema `fanta` del DB della classifica |
| `client/` | SPA Vue 3 + Vite (PWA installabile): al rilascio `npm ci && npm run build`, `dist/` va in `wwwroot` |
| `server/wwwroot/` | pagina segnaposto, sostituita dalla build di Vue |
| `Dockerfile` | come si costruisce l'immagine (di solito non va toccato) |

## API

| Percorso | Cosa |
|---|---|
| `/api/salute` | stato, versione pubblicata, DB raggiungibile |
| `/api/giocatori?tutti=false` | classifica: ELO interno, ranked 1v1 e TG (il più alto tra gli account), Diti, winrate |
| `/api/giocatori/{id}` | dettaglio: account, ultime 30 partite, andamento ELO, civiltà |
| `/api/partite?pagina=1&perPagina=30` | storico partite con i giocatori |
| `/api/partite/{id}` | una partita |
| `/api/civ?giorni=365` | partite e vittorie per civiltà |

In locale: `cd client && npm install && npm run dev` usa le API del sito pubblicato.

Se il progetto server cambia nome, aggiorna `PROGETTO` e l'`ENTRYPOINT` nel `Dockerfile`.

## Regole del server

1. L'app ascolta in **HTTP sulla porta 8080**. L'HTTPS è già davanti: niente `UseHttpsRedirection` né certificati.
2. **Niente file salvati su disco**: il file system è in sola lettura. Tutto va nel database.
3. **Database: PostgreSQL** (EF Core + Npgsql). SQL Server su ARM non gira.
4. **Segreti** (connection string, chiavi OAuth…) solo da **variabili d'ambiente**, mai nel repo. Si leggono normalmente con `builder.Configuration` (es. `ConnectionStrings__Fanta`).
5. Memoria massima 512 MB.

## Prova in locale

```bash
cd server && dotnet run
```

Poi apri http://localhost:5000/api/salute (o la porta indicata da `dotnet run`).
