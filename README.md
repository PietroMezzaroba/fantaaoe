# FantaAoE

Sito del fantaAoE della community Age of Empires Italia.

**Indirizzo:** https://fantaaoe.tail17199a.ts.net

## Come si rilascia

Fai `git push` su `main`: GitHub Actions compila l'immagine per il server (Linux ARM64) e dopo qualche minuto il sito si aggiorna da solo. Docker in locale non serve. L'esito della compilazione si vede nella scheda **Actions** del repo.

## Struttura

| Cartella | Cosa |
|---|---|
| `server/` | API ASP.NET Core **.NET 10** (`FantaAoe.Api.csproj`, scheletro di partenza da sostituire) |
| `server/wwwroot/` | pagina segnaposto, sostituita dalla build di Vue |
| `client/` | SPA Vue: al rilascio `npm ci && npm run build`, `dist/` va in `wwwroot` |
| `Dockerfile` | come si costruisce l'immagine (di solito non va toccato) |

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
