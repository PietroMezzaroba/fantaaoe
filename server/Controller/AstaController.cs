using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using static FantaAoe.Api.TorneoUtil;

namespace FantaAoe.Api.Controllers;

/// <summary>Pagina dell'asta (riscritta da capo). Per ora: le squadre con capitano e giocatori.</summary>
[ApiController]
public class AstaController : ControllerBase
{
    private readonly NpgsqlDataSource? db;
    private readonly IHubContext<AstaHub> hub;

    public AstaController(IHubContext<AstaHub> hub, NpgsqlDataSource? db = null)
    {
        this.db = db;
        this.hub = hub;
    }

    private sealed record RigaSquadra(int Id, string Nome, string? Capitano, int? Credito, string? IdCapitano);
    private sealed record RigaOfferta(string IdCapitano, int Puntata);
    private sealed record RigaGiocatore(int IdSquadra, string IdDiscord, string? Nome, int Elo, int Costo, bool Capitano);

    // L'asta è attiva quando configurazioni.stato_globale_asta vale 'asta attiva'
    [HttpGet("/api/asta/stato")]
    public async Task<IResult> Stato()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);

        await using var c = await db.OpenConnectionAsync();
        var stato = await c.ExecuteScalarAsync<string?>("select valore from fanta_torneo.configurazioni where chiave = 'stato_globale_asta'");
        return Results.Ok(new { attiva = stato == "asta attiva" });
    }

    // Di chi è il turno per la prima chiamata: tra i capitani con un ordine assegnato (> 0) tocca a chi ha fatto meno prime
    // chiamate (asta_giocatori_chiamati.id_chiamato_da); a parità, a chi ha l'ordine più basso (asta_ordine_capitani).
    // I tasti "Offri" sono attivi solo se fase_flusso_asta = 'chiamata_giocatore' e l'utente connesso è questo capitano.
    private sealed record Turno_(bool InChiamata, string? IdCapitano, string? NomeCapitano);

    private static async Task<Turno_> TrovaTurno(NpgsqlConnection c)
    {
        var fase = await c.ExecuteScalarAsync<string?>("select valore from fanta_torneo.configurazioni where chiave = 'fase_flusso_asta'");
        var capitano = await c.QueryFirstOrDefaultAsync<(string IdDiscord, string? Nome)?>("""
            select o.id_utente as iddiscord, u.discord_nome as nome
            from fanta_torneo.asta_ordine_capitani o
            join fanta_torneo.utenti_discord u on u.discord_id = o.id_utente
            where o.ordine > 0
              and exists (select 1
                          from fanta_torneo.utenti_ruoli ur
                          join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                          where ur.id_utente = o.id_utente and r.chiave_ruolo = 'capitano')
            order by (select count(*) from fanta_torneo.asta_giocatori_chiamati ch where ch.id_chiamato_da = o.id_utente), o.ordine
            limit 1
            """);
        return new Turno_(fase == "chiamata_giocatore", capitano?.IdDiscord, capitano?.Nome);
    }

    [HttpGet("/api/asta/turno")]
    public async Task<IResult> Turno()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);

        await using var c = await db.OpenConnectionAsync();
        var t = await TrovaTurno(c);
        var io = EAutenticato(User) ? DiscordId(User) : null;
        var tuoTurno = t.InChiamata && t.IdCapitano is not null && io is not null && t.IdCapitano == io;
        return Results.Ok(new { faseChiamata = t.InChiamata, capitano = t.NomeCapitano, tuoTurno });
    }

    public sealed record RichiestaChiamata(string? IdGiocatore, int? Puntata);

    // Prima chiamata di un giocatore: scrive il record in asta_giocatori_chiamati. Serve che sia il turno del capitano
    // connesso e che la puntata sia >= 0 e non superi il suo credito residuo. Poi la fase del flusso passa a 'fase di acquisto'.
    [HttpPost("/api/asta/chiamata")]
    public async Task<IResult> Chiama(RichiestaChiamata req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        var io = DiscordId(User)!;

        if (string.IsNullOrWhiteSpace(req.IdGiocatore))
            return Results.BadRequest(new { errore = "Giocatore non indicato." });
        if (req.Puntata is not int puntata || puntata < 0)
            return Results.BadRequest(new { errore = "La puntata deve essere un numero intero maggiore o uguale a 0." });

        await using var c = await db.OpenConnectionAsync();
        var t = await TrovaTurno(c);
        if (!t.InChiamata)
            return Results.Json(new { errore = "Non è il momento di chiamare un giocatore." }, statusCode: 409);
        if (t.IdCapitano != io)
            return Results.Json(new { errore = "Non è il tuo turno." }, statusCode: 403);

        var credito = await c.ExecuteScalarAsync<int?>("select credito_residuo from fanta_torneo.asta_portafoglio_utente where id_utente = @io", new { io });
        if (credito is null)
            return Results.BadRequest(new { errore = "Non hai un portafoglio per l'asta." });
        if (puntata > credito)
            return Results.BadRequest(new { errore = $"La puntata supera il tuo credito residuo ({credito})." });

        var disponibile = await c.ExecuteScalarAsync<bool>("""
            select exists(select 1 from fanta_torneo.utenti_iscritti i
                          where i.id_discord = @id and i.stato_validazione = 'validato'
                            and not exists (select 1 from fanta_torneo.asta_giocatori_acquistati a where a.id_iscritto = i.id_discord)
                            and not exists (select 1 from fanta_torneo.asta_giocatori_chiamati h where h.id_utente = i.id_discord))
            """, new { id = req.IdGiocatore });
        if (!disponibile)
            return Results.Json(new { errore = "Il giocatore non è disponibile (non iscritto, non validato o già chiamato)." }, statusCode: 409);

        // chiamata, prima offerta e cambio di fase vanno insieme: o si salva tutto o niente
        await using var tx = await c.BeginTransactionAsync();
        await c.ExecuteAsync("""
            insert into fanta_torneo.asta_giocatori_chiamati (id_utente, id_chiamato_da, stato, costo_iniziale, data_inserimento)
            values (@id, @io, 'fase di acquisto', @puntata, now())
            """, new { id = req.IdGiocatore, io, puntata }, tx);
        // la prima chiamata è anche la prima offerta del giocatore
        await c.ExecuteAsync("""
            insert into fanta_torneo.asta_offerte (id_utente, id_chiamato_da, puntata, data_puntata)
            values (@id, @io, @puntata, now())
            """, new { id = req.IdGiocatore, io, puntata }, tx);
        await c.ExecuteAsync(
            "update fanta_torneo.configurazioni set valore = 'fase di acquisto' where chiave = 'fase_flusso_asta'", transaction: tx);
        await tx.CommitAsync();
        await Notifica(c, "chiamata");
        return Results.Ok(new { ok = true });
    }

    // Dati per il refresh della sezione Puntate. Solo in 'fase di acquisto' (fase_flusso_asta): il giocatore in acquisto
    // (l'unico in asta_giocatori_chiamati con stato 'fase di acquisto'), le sue offerte per capitano (asta_offerte; per ogni
    // capitano le ultime 7, in ordine crescente) e l'offerta più alta.
    private sealed record PuntateCapitano(string IdCapitano, List<int> Valori);
    private sealed record StatoPuntate(string? Fase, string? Giocatore, int EloGiocatore, int PuntataMassima, List<PuntateCapitano> Puntate);

    // Stato della sezione Puntate (vedi sopra): serve sia all'endpoint sia all'evento inviato a tutti i client.
    private static async Task<StatoPuntate> LeggiPuntate(NpgsqlConnection c)
    {
        var fase = await c.ExecuteScalarAsync<string?>("select valore from fanta_torneo.configurazioni where chiave = 'fase_flusso_asta'");
        if (fase != "fase di acquisto") return new StatoPuntate(fase, null, 0, 0, []);

        var giocatore = await c.QueryFirstOrDefaultAsync<(string IdDiscord, string? Nome, int Elo)?>("""
            select h.id_utente as iddiscord, coalesce(i.nickname, u.discord_nome) as nome, coalesce(i.elo_calcolato, 0) as elo
            from fanta_torneo.asta_giocatori_chiamati h
            join fanta_torneo.utenti_discord u on u.discord_id = h.id_utente
            left join fanta_torneo.utenti_iscritti i on i.id_discord = h.id_utente
            where h.stato = 'fase di acquisto'
            order by h.data_inserimento desc
            limit 1
            """);
        if (giocatore is null) return new StatoPuntate(fase, null, 0, 0, []);

        var offerte = (await c.QueryAsync<RigaOfferta>(
            "select id_chiamato_da as idcapitano, puntata from fanta_torneo.asta_offerte where id_utente = @id order by id_incrementale",
            new { id = giocatore.Value.IdDiscord })).ToList();
        return new StatoPuntate(
            fase, giocatore.Value.Nome, giocatore.Value.Elo,
            offerte.Count == 0 ? 0 : offerte.Max(o => o.Puntata),
            offerte.GroupBy(o => o.IdCapitano).Select(g => new PuntateCapitano(g.Key, g.TakeLast(7).Select(o => o.Puntata).Order().ToList())).ToList());
    }

    // Avvisa tutti i client connessi: tipo "chiamata" (nuovo giocatore in acquisto) o "offerta"; con lo stato delle puntate già pronto.
    private async Task Notifica(NpgsqlConnection c, string tipo)
    {
        var p = await LeggiPuntate(c);
        await hub.Clients.All.SendAsync(AstaHub.EventoAggiornata, new
        {
            tipo,
            puntate = new { p.Fase, giocatore = p.Giocatore, eloGiocatore = p.EloGiocatore, puntataMassima = p.PuntataMassima, puntate = p.Puntate.Select(x => new { idCapitano = x.IdCapitano, valori = x.Valori }) },
        });
    }

    [HttpGet("/api/asta/puntate")]
    public async Task<IResult> Puntate()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);

        await using var c = await db.OpenConnectionAsync();
        var p = await LeggiPuntate(c);
        return Results.Ok(new { fase = p.Fase, giocatore = p.Giocatore, eloGiocatore = p.EloGiocatore, puntataMassima = p.PuntataMassima, puntate = p.Puntate.Select(x => new { idCapitano = x.IdCapitano, valori = x.Valori }) });
    }

    public sealed record RichiestaOfferta(int? Incremento, int? Importo);

    // Offerta di un capitano sul giocatore in 'fase di acquisto': o un rialzo (+N sulla puntata massima attuale) o un importo.
    // Deve superare la puntata massima e non il credito residuo del capitano. Scrive il record in asta_offerte.
    [HttpPost("/api/asta/offerta")]
    public async Task<IResult> Offri(RichiestaOfferta req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        var io = DiscordId(User)!;
        if ((req.Incremento is null) == (req.Importo is null))
            return Results.BadRequest(new { errore = "Indica o un rialzo o un importo." });
        if (req.Incremento is <= 0 || req.Importo is < 0)
            return Results.BadRequest(new { errore = "L'offerta deve essere un numero intero positivo." });

        await using var c = await db.OpenConnectionAsync();
        if (!await c.ExecuteScalarAsync<bool>("""
                select exists(select 1 from fanta_torneo.utenti_ruoli ur
                              join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                              where ur.id_utente = @io and r.chiave_ruolo = 'capitano')
                """, new { io }))
            return Results.Json(new { errore = "Solo i capitani possono fare offerte." }, statusCode: 403);

        // una transazione con blocco, così due offerte contemporanee non si calpestano
        await using var tx = await c.BeginTransactionAsync();
        await c.ExecuteAsync("select pg_advisory_xact_lock(727001)", transaction: tx);

        var fase = await c.ExecuteScalarAsync<string?>("select valore from fanta_torneo.configurazioni where chiave = 'fase_flusso_asta'", transaction: tx);
        var giocatore = fase == "fase di acquisto"
            ? await c.ExecuteScalarAsync<string?>("select id_utente from fanta_torneo.asta_giocatori_chiamati where stato = 'fase di acquisto' order by data_inserimento desc limit 1", transaction: tx)
            : null;
        if (giocatore is null)
            return Results.Json(new { errore = "Non c'è nessun giocatore in fase di acquisto." }, statusCode: 409);

        var massima = await c.ExecuteScalarAsync<int>("select coalesce(max(puntata), 0) from fanta_torneo.asta_offerte where id_utente = @giocatore", new { giocatore }, tx);
        var puntata = req.Incremento is int inc ? massima + inc : req.Importo!.Value;
        if (puntata <= massima)
            return Results.BadRequest(new { errore = $"L'offerta deve superare la puntata massima attuale ({massima})." });

        var credito = await c.ExecuteScalarAsync<int?>("select credito_residuo from fanta_torneo.asta_portafoglio_utente where id_utente = @io", new { io }, tx);
        if (credito is null)
            return Results.BadRequest(new { errore = "Non hai un portafoglio per l'asta." });
        if (puntata > credito)
            return Results.BadRequest(new { errore = $"L'offerta ({puntata}) supera il tuo credito residuo ({credito})." });

        await c.ExecuteAsync("""
            insert into fanta_torneo.asta_offerte (id_utente, id_chiamato_da, puntata, data_puntata)
            values (@giocatore, @io, @puntata, now())
            """, new { giocatore, io, puntata }, tx);
        await tx.CommitAsync();
        await Notifica(c, "offerta");
        return Results.Ok(new { ok = true, puntata });
    }

    // Le squadre con il nome del capitano e i loro giocatori (asta_giocatori_acquistati): per ognuno nome, ELO e costo
    // di acquisto. Il capitano è anche un giocatore (a costo 0). L'ordine è per ELO decrescente.
    [HttpGet("/api/asta/squadre")]
    public async Task<IResult> Squadre()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);

        await using var c = await db.OpenConnectionAsync();
        var squadre = (await c.QueryAsync<RigaSquadra>("""
            select s.id, s.nome, u.discord_nome as capitano, p.credito_residuo as credito, s.id_capitano as idcapitano
            from fanta_torneo.squadre s
            left join fanta_torneo.utenti_discord u on u.discord_id = s.id_capitano
            left join fanta_torneo.asta_portafoglio_utente p on p.id_utente = s.id_capitano
            order by s.id
            """)).ToList();
        var giocatori = (await c.QueryAsync<RigaGiocatore>("""
            select a.id_squadra as idsquadra, a.id_iscritto as iddiscord, i.nickname as nome, i.elo_calcolato as elo, a.costo_acquisto as costo,
                   (a.id_iscritto = s.id_capitano) as capitano
            from fanta_torneo.asta_giocatori_acquistati a
            join fanta_torneo.squadre s on s.id = a.id_squadra
            join fanta_torneo.utenti_iscritti i on i.id_discord = a.id_iscritto
            order by i.elo_calcolato desc, a.id
            """)).ToLookup(g => g.IdSquadra);

        // "tu": il giocatore è l'utente connesso (si manda solo questo, non gli id degli altri)
        var io = EAutenticato(User) ? DiscordId(User) : null;

        return Results.Ok(squadre.Select(s => new
        {
            s.Id,
            s.Nome,
            s.Capitano,
            s.IdCapitano,
            s.Credito, // crediti rimasti: asta_portafoglio_utente.credito_residuo del capitano (null se non ha un portafoglio)
            Giocatori = giocatori[s.Id].Select(g => new { Nome = g.Nome ?? "", g.Elo, g.Costo, g.Capitano, Tu = io is not null && g.IdDiscord == io }).ToList(),
        }));
    }

    // Giocatori ancora disponibili: gli iscritti validati non ancora in nessuna squadra né già chiamati (asta_giocatori_acquistati), per ELO decrescente.
    [HttpGet("/api/asta/giocatori-disponibili")]
    public async Task<IResult> GiocatoriDisponibili()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync("""
            select i.id_discord as "idDiscord", coalesce(i.nickname, '') as nome, i.elo_calcolato as elo
            from fanta_torneo.utenti_iscritti i
            where i.stato_validazione = 'validato'
              and not exists (select 1 from fanta_torneo.asta_giocatori_acquistati a where a.id_iscritto = i.id_discord)
              and not exists (select 1 from fanta_torneo.asta_giocatori_chiamati h where h.id_utente = i.id_discord)
            order by i.elo_calcolato desc, lower(i.nickname), i.id_discord
            """);
        return Results.Ok(righe);
    }
}
