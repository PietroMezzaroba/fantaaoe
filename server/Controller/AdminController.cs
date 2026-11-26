using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using static FantaAoe.Api.TorneoUtil;

namespace FantaAoe.Api.Controllers;

public sealed record RichiestaOrdineCapitano(string? IdUtente, int Ordine);

public sealed record RichiestaFlussoAsta(List<RichiestaOrdineCapitano>? Ordini);

public sealed record RichiestaModificaIscritto(string? StatoValidazione, string? Note);

public sealed record RichiestaConfigurazione(string? Valore);

public sealed record RichiestaUtente(bool? Verificato, string[]? Ruoli);

public sealed record RichiestaNomeSquadra(string? Nome, string? IdCapitano = null);

/// <summary>Area di gestione riservata agli admin: squadre, iscritti (lettura) e utenti (verifica e ruoli).</summary>
[ApiController]
public class AdminController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public AdminController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    [HttpGet("/api/admin/squadre")]
    public async Task<IResult> Squadre()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync("""
            select s.id, s.nome, s.id_capitano as "idCapitano", cap.discord_nome as "capitanoNome",
                   (select count(*) from fanta_torneo.asta_giocatori_acquistati a where a.id_squadra = s.id)::int as "nGiocatori",
                   (select count(*) from fanta_torneo.asta_giocatori_acquistati a where a.id_squadra = s.id)::int as "nAcquistati"
            from fanta_torneo.squadre s
            left join fanta_torneo.utenti_discord cap on cap.discord_id = s.id_capitano
            order by s.id
            """);
        return Results.Ok(righe);
    }

    [HttpGet("/api/admin/utenti-iscritti")]
    public async Task<IResult> UtentiIscritti()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync("""
            select i.id_discord as "idDiscord", i.id_squadra as "idSquadra", s.nome as "squadraNome", i.nickname, rr.chiavi as "ruoli", rr.nomi as "ruoliNomi", i.fascia, i.seeding,
                   i.prezzo_base as "prezzoBase", i.prezzo_asta as "prezzoAsta", i.elo_calcolato as "eloCalcolato", i.stato, i.note,
                   i.data_iscrizione as "dataIscrizione", i.stato_validazione as "statoValidazione"
            from fanta_torneo.utenti_iscritti i
            left join fanta_torneo.squadre s on s.id = i.id_squadra
            left join lateral (
                select array_agg(r.chiave_ruolo order by r.id_ruolo) as chiavi, array_agg(r.nome order by r.id_ruolo) as nomi
                from fanta_torneo.utenti_ruoli ur
                join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                where ur.id_utente = i.id_discord
            ) rr on true
            order by i.data_iscrizione, i.id_discord
            """);
        return Results.Ok(righe);
    }

    // Ruoli possibili: servono alla form degli utenti (una checkbox per ruolo)
    [HttpGet("/api/admin/ruoli")]
    public async Task<IResult> Ruoli()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync(
            "select id_ruolo as \"idRuolo\", chiave_ruolo as \"chiaveRuolo\", nome from fanta_torneo.ruoli order by id_ruolo");
        return Results.Ok(righe);
    }

    [HttpGet("/api/admin/utenti")]
    public async Task<IResult> Utenti()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync("""
            select u.discord_id as "idDiscord", u.discord_nome as nome, u.verificato,
                   rr.chiavi as "ruoli", rr.nomi as "ruoliNomi"
            from fanta_torneo.utenti_discord u
            left join lateral (
                select array_agg(r.chiave_ruolo order by r.id_ruolo) as chiavi, array_agg(r.nome order by r.id_ruolo) as nomi
                from fanta_torneo.utenti_ruoli ur
                join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                where ur.id_utente = u.discord_id
            ) rr on true
            order by lower(u.discord_nome), u.discord_id
            """);
        return Results.Ok(righe);
    }

    // Modifica di un utente: flag "verificato" e insieme dei ruoli (quelli non indicati vengono tolti).
    // Campi omessi (null) = invariati.
    [HttpPut("/api/admin/utenti/{id}")]
    public async Task<IResult> ModificaUtente(string id, RichiestaUtente req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        if (!await c.ExecuteScalarAsync<bool>("select exists(select 1 from fanta_torneo.utenti_discord where discord_id = @id)", new { id }))
            return Results.NotFound(new { errore = "Utente non trovato." });

        string[]? ruoli = req.Ruoli?.Select(r => r.Trim()).Where(r => r.Length > 0).Distinct().ToArray();
        if (ruoli is not null)
        {
            var noti = (await c.QueryAsync<string>("select chiave_ruolo from fanta_torneo.ruoli where chiave_ruolo = any(@ruoli)", new { ruoli })).ToHashSet();
            var sconosciuti = ruoli.Where(r => !noti.Contains(r)).ToArray();
            if (sconosciuti.Length > 0)
                return Results.BadRequest(new { errore = $"Ruolo non valido: {string.Join(", ", sconosciuti)}." });

            // per non restare senza admin: ognuno può togliere l'admin agli altri, ma non a se stesso
            if (id == DiscordId(User) && !ruoli.Contains(Accesso.Admin))
                return Results.BadRequest(new { errore = "Non puoi togliere a te stesso il ruolo admin." });

            // niente capitani "orfani": chi guida una squadra non perde il ruolo capitano finché la guida
            if (!ruoli.Contains(Accesso.Capitano))
            {
                var squadre = (await c.QueryAsync<string>(
                    "select nome from fanta_torneo.squadre where id_capitano = @id order by nome", new { id })).ToList();
                if (squadre.Count > 0)
                    return Results.Conflict(new { errore = $"Non puoi togliere il ruolo capitano: è capitano di {string.Join(", ", squadre)}. Cambia prima il capitano della squadra." });
            }
        }

        await using var tx = await c.BeginTransactionAsync();
        if (req.Verificato is bool verificato)
            await c.ExecuteAsync("update fanta_torneo.utenti_discord set verificato = @verificato, aggiornato_il = now() where discord_id = @id",
                new { id, verificato }, tx);
        if (ruoli is not null)
        {
            await c.ExecuteAsync("""
                delete from fanta_torneo.utenti_ruoli
                where id_utente = @id
                  and id_ruolo not in (select id_ruolo from fanta_torneo.ruoli where chiave_ruolo = any(@ruoli))
                """, new { id, ruoli }, tx);
            await c.ExecuteAsync("""
                insert into fanta_torneo.utenti_ruoli (id_utente, id_ruolo)
                select @id, id_ruolo from fanta_torneo.ruoli where chiave_ruolo = any(@ruoli)
                on conflict (id_utente, id_ruolo) do nothing
                """, new { id, ruoli }, tx);
        }
        await tx.CommitAsync();
        return Results.Ok(new { ok = true });
    }

    // Portafoglio d'asta dei capitani: solo gli utenti col ruolo "capitano". Chi non ha ancora una riga in
    // asta_portafoglio_utente compare lo stesso, con i crediti vuoti.
    // Flusso dell'asta: ordine con cui i capitani chiamano i giocatori.
    // ordine 0 = non ancora assegnato; da 1 a N (N = numero di capitani) le posizioni, senza ripetizioni.
    private const string SqlCapitani = """
        select u.discord_id, u.discord_nome
        from fanta_torneo.utenti_discord u
        where exists (select 1
                      from fanta_torneo.utenti_ruoli ur
                      join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                      where ur.id_utente = u.discord_id and r.chiave_ruolo = 'capitano')
        """;

    [HttpGet("/api/admin/flusso-asta")]
    public async Task<IResult> FlussoAsta()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync($"""
            select cap.discord_id as "idDiscord", cap.discord_nome as nome, coalesce(o.ordine, 0) as ordine,
                   i.elo_calcolato as "eloCalcolato" -- null se il capitano non è iscritto
            from ({SqlCapitani}) cap
            left join fanta_torneo.asta_ordine_capitani o on o.id_utente = cap.discord_id
            left join fanta_torneo.utenti_iscritti i on i.id_discord = cap.discord_id
            order by (coalesce(o.ordine, 0) = 0), coalesce(o.ordine, 0), lower(cap.discord_nome), cap.discord_id
            """);
        return Results.Ok(righe);
    }

    [HttpPut("/api/admin/flusso-asta")]
    public async Task<IResult> SalvaFlussoAsta(RichiestaFlussoAsta req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        var ordini = req.Ordini ?? [];
        await using var c = await db.OpenConnectionAsync();
        var capitani = (await c.QueryAsync<string>($"select cap.discord_id from ({SqlCapitani}) cap")).ToHashSet();
        var n = capitani.Count;

        if (ordini.Any(o => string.IsNullOrWhiteSpace(o.IdUtente) || !capitani.Contains(o.IdUtente)))
            return Results.BadRequest(new { errore = "L'elenco contiene un utente che non è un capitano." });
        if (ordini.Select(o => o.IdUtente).Distinct().Count() != ordini.Count)
            return Results.BadRequest(new { errore = "Un capitano compare più volte." });
        if (ordini.Any(o => o.Ordine < 0 || o.Ordine > n))
            return Results.BadRequest(new { errore = $"L'ordine deve essere un numero da 0 a {n}." });
        var ripetuti = ordini.Where(o => o.Ordine > 0).GroupBy(o => o.Ordine).Where(g => g.Count() > 1).Select(g => g.Key).OrderBy(x => x).ToList();
        if (ripetuti.Count > 0)
            return Results.BadRequest(new { errore = $"Posizione assegnata a più capitani: {string.Join(", ", ripetuti)}. Ogni posizione da 1 a {n} può essere usata una volta sola (0 = non ordinato)." });

        await using var tx = await c.BeginTransactionAsync();
        foreach (var o in ordini)
        {
            await c.ExecuteAsync("""
                insert into fanta_torneo.asta_ordine_capitani (id_utente, ordine)
                values (@idUtente, @ordine)
                on conflict (id_utente) do update set ordine = excluded.ordine
                """, new { idUtente = o.IdUtente, ordine = o.Ordine }, tx);
        }
        await tx.CommitAsync();
        return Results.Ok(new { ok = true });
    }

    // Il credito iniziale non è fisso nel codice: sta in fanta_torneo.configurazioni, chiave "credito_iniziale".
    private const string ChiaveCreditoIniziale = "credito_iniziale";

    // Configurazioni globali
    [HttpGet("/api/admin/configurazioni")]
    public async Task<IResult> Configurazioni()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync("select chiave, valore, descrizione from fanta_torneo.configurazioni order by chiave");
        return Results.Ok(righe);
    }

    private const int LunghezzaMaxValoreConfigurazione = 1000;

    // Configurazioni con limiti particolari (le altre accettano qualsiasi testo): vanno tenute allineate a regoleConfigurazioni in Admin.vue
    private static readonly Dictionary<string, string[]> ConfigurazioniASceltaFissa = new()
    {
        ["fase_flusso_asta"] = ["chiamata_giocatore", "fase di acquisto", "acquisto fatto"],
        ["stato_globale_asta"] = ["iscrizione", "fine iscrizione", "asta attiva", "asta terminata", "torneo avviato - fase 1"],
    };
    private static readonly Dictionary<string, (int Min, int Max)> ConfigurazioniNumeriInteri = new()
    {
        ["credito_iniziale"] = (0, 1000),
        ["timer_asta_secondi"] = (1, 200),
    };

    // Restituisce il testo dell'errore se il valore non è ammesso per quella configurazione, altrimenti null
    private static string? ErroreValoreConfigurazione(string chiave, string valore)
    {
        if (ConfigurazioniASceltaFissa.TryGetValue(chiave, out var ammessi) && !ammessi.Contains(valore))
            return $"Il valore di \"{chiave}\" può essere solo: {string.Join(", ", ammessi.Select(a => $"\"{a}\""))}.";
        if (ConfigurazioniNumeriInteri.TryGetValue(chiave, out var limiti)
            && !(int.TryParse(valore, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= limiti.Min && n <= limiti.Max))
            return $"Il valore di \"{chiave}\" deve essere un numero intero da {limiti.Min} a {limiti.Max}.";
        return null;
    }

    // Si modifica solo il valore: niente creazione, niente eliminazione, chiave e descrizione restano quelle del database.
    [HttpPut("/api/admin/configurazioni/{chiave}")]
    public async Task<IResult> ModificaConfigurazione(string chiave, RichiestaConfigurazione req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        if (req.Valore is null)
            return Results.BadRequest(new { errore = "Il valore è obbligatorio." });
        if (req.Valore.Length > LunghezzaMaxValoreConfigurazione)
            return Results.BadRequest(new { errore = $"Il valore può essere lungo al massimo {LunghezzaMaxValoreConfigurazione} caratteri." });

        if (ErroreValoreConfigurazione(chiave, req.Valore) is { } erroreValore)
            return Results.BadRequest(new { errore = erroreValore });

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.ExecuteAsync(
            "update fanta_torneo.configurazioni set valore = @valore where chiave = @chiave", new { valore = req.Valore, chiave });
        return righe == 0 ? Results.NotFound(new { errore = "Configurazione non trovata." }) : Results.Ok(new { ok = true });
    }

    [HttpGet("/api/admin/portafoglio-capitani")]
    public async Task<IResult> PortafoglioCapitani()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync("""
            select u.discord_id as "idDiscord", u.discord_nome as nome,
                   p.credito_iniziale as "creditoIniziale", p.credito_residuo as "creditoResiduo"
            from fanta_torneo.utenti_discord u
            left join fanta_torneo.asta_portafoglio_utente p on p.id_utente = u.discord_id
            where exists (select 1
                          from fanta_torneo.utenti_ruoli ur
                          join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                          where ur.id_utente = u.discord_id and r.chiave_ruolo = 'capitano')
            order by lower(u.discord_nome), u.discord_id
            """);
        return Results.Ok(righe);
    }

    // Riporta credito iniziale e residuo del capitano al credito iniziale configurato (se non ha ancora un portafoglio lo crea)
    [HttpPost("/api/admin/portafoglio-capitani/{id}/reset")]
    public async Task<IResult> ResettaCredito(string id)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var eCapitano = await c.ExecuteScalarAsync<bool>("""
            select exists(select 1
                          from fanta_torneo.utenti_ruoli ur
                          join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                          where ur.id_utente = @id and r.chiave_ruolo = 'capitano')
            """, new { id });
        if (!eCapitano) return Results.NotFound(new { errore = "L'utente non è un capitano." });

        var valore = await c.ExecuteScalarAsync<string?>(
            "select valore from fanta_torneo.configurazioni where chiave = @chiave", new { chiave = ChiaveCreditoIniziale });
        if (!int.TryParse(valore, out var credito) || credito < 0)
            return Results.Problem($"La configurazione \"{ChiaveCreditoIniziale}\" manca o non è un numero valido.", statusCode: 500);

        await c.ExecuteAsync("""
            insert into fanta_torneo.asta_portafoglio_utente (id_utente, credito_iniziale, credito_residuo)
            values (@id, @credito, @credito)
            on conflict (id_utente) do update
            set credito_iniziale = excluded.credito_iniziale, credito_residuo = excluded.credito_residuo
            """, new { id, credito });
        return Results.Ok(new { ok = true, credito });
    }

    private static readonly string[] StatiValidazione = ["in_validazione", "validato"];
    private const int LunghezzaMaxNote = 1000;

    // Modifica di un iscritto: per ora solo la validazione e le note (campi omessi = invariati)
    [HttpPut("/api/admin/utenti-iscritti/{id}")]
    public async Task<IResult> ModificaIscritto(string id, RichiestaModificaIscritto req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        if (req.StatoValidazione is not null && !StatiValidazione.Contains(req.StatoValidazione))
            return Results.BadRequest(new { errore = "Stato di validazione non valido." });
        var note = req.Note?.Trim();
        if (note is not null && note.Length > LunghezzaMaxNote)
            return Results.BadRequest(new { errore = $"Le note possono avere al massimo {LunghezzaMaxNote} caratteri." });

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.ExecuteAsync("""
            update fanta_torneo.utenti_iscritti
            set stato_validazione = coalesce(@statoValidazione, stato_validazione),
                note = coalesce(@note, note)
            where id_discord = @id
            """, new { id, statoValidazione = req.StatoValidazione, note });
        if (righe == 0) return Results.NotFound(new { errore = "Iscritto non trovato." });
        return Results.Ok(new { ok = true });
    }

    private const int LunghezzaMaxNome = 100;

    private static string? ValidaNome(RichiestaNomeSquadra req, out string nome)
    {
        nome = (req.Nome ?? "").Trim();
        if (nome.Length == 0) return "Il nome della squadra è obbligatorio.";
        if (nome.Length > LunghezzaMaxNome) return $"Il nome può avere al massimo {LunghezzaMaxNome} caratteri.";
        return null;
    }

    // il capitano, se indicato, dev'essere un utente con il ruolo 'capitano' (utenti_ruoli), iscritto o no; stringa vuota = nessun capitano
    private static async Task<(string? Id, string? Errore)> ValidaCapitano(NpgsqlConnection c, RichiestaNomeSquadra req, string? attuale = null)
    {
        var id = string.IsNullOrWhiteSpace(req.IdCapitano) ? null : req.IdCapitano.Trim();
        if (id is null || id == attuale) return (id, null); // invariato: non lo ricontrollo
        var esiste = await c.ExecuteScalarAsync<bool>(
            """
            select exists(select 1 from fanta_torneo.utenti_ruoli ur
                          join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                          where ur.id_utente = @id and r.chiave_ruolo = 'capitano')
            """, new { id });
        return esiste ? (id, null) : (null, "Il capitano deve essere un utente con il ruolo \"capitano\".");
    }

    // Il capitano entra tra i giocatori della squadra (asta_giocatori_acquistati) a costo 0: dev'essere un iscritto
    // e non già in un'altra squadra (un giocatore si acquista una volta sola). Restituisce (codice, errore) o null se va bene.
    private static async Task<(int Codice, string Errore)?> ControllaCapitanoInSquadra(NpgsqlConnection c, string capitano, int? idSquadra)
    {
        if (!await c.ExecuteScalarAsync<bool>("select exists(select 1 from fanta_torneo.utenti_iscritti where id_discord = @capitano)", new { capitano }))
            return (400, "Il capitano deve essere anche iscritto: serve per inserirlo tra i giocatori della squadra.");
        var altra = await c.ExecuteScalarAsync<string?>("""
            select s.nome
            from fanta_torneo.asta_giocatori_acquistati a
            join fanta_torneo.squadre s on s.id = a.id_squadra
            where a.id_iscritto = @capitano and a.id_squadra is distinct from @idSquadra
            """, new { capitano, idSquadra });
        if (altra is not null)
            return (409, $"Il capitano scelto è già tra i giocatori di un'altra squadra ({altra}).");
        return null;
    }

    [HttpPost("/api/admin/squadre")]
    public async Task<IResult> CreaSquadra(RichiestaNomeSquadra req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();
        if (ValidaNome(req, out var nome) is { } errore) return Results.BadRequest(new { errore });

        await using var c = await db.OpenConnectionAsync();
        var (capitano, erroreCapitano) = await ValidaCapitano(c, req);
        if (erroreCapitano is not null) return Results.BadRequest(new { errore = erroreCapitano });
        if (capitano is not null && await ControllaCapitanoInSquadra(c, capitano, null) is { } problema)
            return Results.Json(new { errore = problema.Errore }, statusCode: problema.Codice);

        await using var tx = await c.BeginTransactionAsync();
        var id = await c.ExecuteScalarAsync<int>(
            "insert into fanta_torneo.squadre (nome, id_capitano) values (@nome, @capitano) returning id", new { nome, capitano }, tx);
        if (capitano is not null)
            await c.ExecuteAsync(
                "insert into fanta_torneo.asta_giocatori_acquistati (id_squadra, id_iscritto, costo_acquisto) values (@id, @capitano, 0)",
                new { id, capitano }, tx);
        await tx.CommitAsync();
        return Results.Ok(new { id, nome });
    }

    [HttpPut("/api/admin/squadre/{id:int}")]
    public async Task<IResult> ModificaSquadra(int id, RichiestaNomeSquadra req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();
        if (ValidaNome(req, out var nome) is { } errore) return Results.BadRequest(new { errore });

        await using var c = await db.OpenConnectionAsync();
        if (!await c.ExecuteScalarAsync<bool>("select exists(select 1 from fanta_torneo.squadre where id = @id)", new { id }))
            return Results.NotFound(new { errore = "Squadra non trovata." });
        var attuale = await c.ExecuteScalarAsync<string?>("select id_capitano from fanta_torneo.squadre where id = @id", new { id });
        var (capitano, erroreCapitano) = await ValidaCapitano(c, req, attuale);
        if (erroreCapitano is not null) return Results.BadRequest(new { errore = erroreCapitano });
        if (capitano is not null && await ControllaCapitanoInSquadra(c, capitano, id) is { } problema)
            return Results.Json(new { errore = problema.Errore }, statusCode: problema.Codice);

        await using var tx = await c.BeginTransactionAsync();
        await c.ExecuteAsync(
            "update fanta_torneo.squadre set nome = @nome, id_capitano = @capitano where id = @id", new { id, nome, capitano }, tx);
        // se il capitano cambia (o viene tolto), esce dai giocatori il vecchio capitano, ma solo se è lì a costo 0 (cioè perché capitano)
        if (attuale is not null && attuale != capitano)
            await c.ExecuteAsync(
                "delete from fanta_torneo.asta_giocatori_acquistati where id_squadra = @id and id_iscritto = @attuale and costo_acquisto = 0",
                new { id, attuale }, tx);
        // il nuovo capitano entra a costo 0 (se c'è già in questa squadra, resta com'è)
        if (capitano is not null)
            await c.ExecuteAsync("""
                insert into fanta_torneo.asta_giocatori_acquistati (id_squadra, id_iscritto, costo_acquisto)
                values (@id, @capitano, 0)
                on conflict (id_iscritto) do nothing
                """, new { id, capitano }, tx);
        await tx.CommitAsync();
        return Results.Ok(new { id, nome });
    }

    // Eliminando una squadra si cancellano anche i giocatori acquistati da lei (asta_giocatori_acquistati). Gli iscritti
    // assegnati alla squadra con utenti_iscritti.id_squadra restano un blocco: nessuna cascata su quelli.
    [HttpDelete("/api/admin/squadre/{id:int}")]
    public async Task<IResult> EliminaSquadra(int id)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        if (!await c.ExecuteScalarAsync<bool>("select exists(select 1 from fanta_torneo.squadre where id = @id)", new { id }))
            return Results.NotFound(new { errore = "Squadra non trovata." });

        var usi = (await c.QueryAsync<(string Dove, int N)>("""
            select 'iscritti' as dove, count(*)::int as n from fanta_torneo.utenti_iscritti where id_squadra = @id
            """, new { id })).Where(u => u.N > 0).ToList();
        if (usi.Count > 0)
        {
            var dettaglio = string.Join(", ", usi.Select(u => $"{u.Dove}: {u.N}"));
            return Results.Conflict(new { errore = $"Non puoi eliminare la squadra: ci sono dati collegati ({dettaglio})." });
        }

        try
        {
            await using var tx = await c.BeginTransactionAsync();
            await c.ExecuteAsync("delete from fanta_torneo.asta_giocatori_acquistati where id_squadra = @id", new { id }, tx);
            await c.ExecuteAsync("delete from fanta_torneo.squadre where id = @id", new { id }, tx);
            await tx.CommitAsync();
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // rete di sicurezza: una tabella che non conosciamo punta alla squadra
            return Results.Conflict(new { errore = "Non puoi eliminare la squadra: ci sono dati collegati." });
        }
        return Results.Ok(new { ok = true });
    }
}
