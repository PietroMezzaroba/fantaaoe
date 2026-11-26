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
    private readonly TimerAsta timer;

    public AstaController(IHubContext<AstaHub> hub, TimerAsta timer, NpgsqlDataSource? db = null)
    {
        this.timer = timer;
        this.db = db;
        this.hub = hub;
    }

    private sealed record RigaSquadra(int Id, string Nome, string? Capitano, int? Credito, string? IdCapitano, int Ordine);
    private sealed record RigaOfferta(string IdCapitano, int Puntata);
    private sealed record RigaGiocatore(int IdSquadra, string IdDiscord, string? Nome, int Elo, int Costo, bool Capitano, int IdAcquisto, bool Eliminabile);

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
        var scadenza = await ImpostaScadenza(c, tx);
        await tx.CommitAsync();
        timer.Pianifica(scadenza);
        await Notifica(c, "chiamata");
        return Results.Ok(new { ok = true });
    }

    // Dati per il refresh della sezione Puntate. Solo in 'fase di acquisto' (fase_flusso_asta): il giocatore in acquisto
    // (l'unico in asta_giocatori_chiamati con stato 'fase di acquisto'), le sue offerte per capitano (asta_offerte; per ogni
    // capitano le ultime 7, in ordine crescente) e l'offerta più alta.
    private sealed record PuntateCapitano(string IdCapitano, List<int> Valori);
    // IdChiamante (la stella): prima della chiamata il capitano di turno, poi quello che ha fatto la prima offerta; IdMigliore: capitano con la puntata più alta
    private sealed record RigaLog(string Tipo, string? Capitano, string? Giocatore, int Valore, DateTime Quando);
    private sealed record StatoPuntate(string? Fase, string? Giocatore, int EloGiocatore, int PuntataMassima, List<PuntateCapitano> Puntate, string? IdChiamante = null, string? IdMigliore = null, List<RigaLog>? Log = null, long? ScadenzaMs = null, long? ResiduoMs = null);

    // Stato della sezione Puntate (vedi sopra): serve sia all'endpoint sia all'evento inviato a tutti i client.
    private static async Task<StatoPuntate> LeggiPuntate(NpgsqlConnection c)
    {
        var p = await LeggiPuntateBase(c);
        var inAcquisto = p.Fase == "fase di acquisto";
        return p with
        {
            Log = await LeggiLog(c),
            ScadenzaMs = inAcquisto ? await LeggiScadenzaMs(c) : null,
            ResiduoMs = inAcquisto ? await LeggiResiduoMs(c) : null,
        };
    }

    // Scadenza del timer dell'asta (millisecondi Unix), se c'è
    private static Task<long?> LeggiScadenzaMs(NpgsqlConnection c) =>
        c.ExecuteScalarAsync<long?>("select (extract(epoch from valore::timestamptz) * 1000)::bigint from fanta_torneo.configurazioni where chiave = 'scadenza_asta'");

    // Timer in pausa: millisecondi che restavano quando è stato fermato (null = non in pausa)
    private static Task<long?> LeggiResiduoMs(NpgsqlConnection c) =>
        c.ExecuteScalarAsync<long?>("select valore::bigint from fanta_torneo.configurazioni where chiave = 'timer_residuo_ms'");

    // Riporta il timer al valore iniziale (timer_asta_secondi, di base 120): da ogni chiamata e da ogni offerta. Restituisce la nuova scadenza.
    private static async Task<DateTimeOffset> ImpostaScadenza(NpgsqlConnection c, NpgsqlTransaction tx)
    {
        await c.ExecuteAsync("delete from fanta_torneo.configurazioni where chiave = 'timer_residuo_ms'", transaction: tx);
        var secondi = int.TryParse(await c.ExecuteScalarAsync<string?>("select valore from fanta_torneo.configurazioni where chiave = 'timer_asta_secondi'", transaction: tx), out var n) && n > 0 ? n : 120;
        var ms = await c.ExecuteScalarAsync<long>("""
            insert into fanta_torneo.configurazioni (chiave, valore, descrizione)
            values ('scadenza_asta', (now() + make_interval(secs => @secondi))::text, 'Quando scade il timer dell''offerta in corso (si imposta a ogni chiamata e offerta)')
            on conflict (chiave) do update set valore = excluded.valore
            returning (extract(epoch from valore::timestamptz) * 1000)::bigint
            """, new { secondi }, tx);
        return DateTimeOffset.FromUnixTimeMilliseconds(ms);
    }

    // Ultimi fatti dell'asta per il riquadro "stato dell'asta", dal più recente: chiamate, offerte (la prima offerta di un giocatore
    // è la chiamata stessa, non si ripete) e acquisti conclusi.
    private static async Task<List<RigaLog>> LeggiLog(NpgsqlConnection c) =>
        (await c.QueryAsync<RigaLog>("""
            select tipo, capitano, giocatore, valore, quando from (
                select 'chiamata' as tipo, cu.discord_nome as capitano, coalesce(i.nickname, gu.discord_nome) as giocatore,
                       h.costo_iniziale as valore, h.data_inserimento as quando, 2 as ordine
                from fanta_torneo.asta_giocatori_chiamati h
                join fanta_torneo.utenti_discord cu on cu.discord_id = h.id_chiamato_da
                join fanta_torneo.utenti_discord gu on gu.discord_id = h.id_utente
                left join fanta_torneo.utenti_iscritti i on i.id_discord = h.id_utente
                union all
                select 'offerta', cu.discord_nome, coalesce(i.nickname, gu.discord_nome), o.puntata, o.data_puntata, 1
                from fanta_torneo.asta_offerte o
                join fanta_torneo.utenti_discord cu on cu.discord_id = o.id_chiamato_da
                join fanta_torneo.utenti_discord gu on gu.discord_id = o.id_utente
                left join fanta_torneo.utenti_iscritti i on i.id_discord = o.id_utente
                where o.id_incrementale <> (select min(x.id_incrementale) from fanta_torneo.asta_offerte x where x.id_utente = o.id_utente)
                union all
                select 'acquisto', cu.discord_nome, coalesce(i.nickname, gu.discord_nome), a.costo_acquisto, a.data_acquisto, 0
                from fanta_torneo.asta_giocatori_acquistati a
                join fanta_torneo.asta_giocatori_chiamati h on h.id_utente = a.id_iscritto and h.stato = 'acquistato'
                join fanta_torneo.squadre s on s.id = a.id_squadra
                join fanta_torneo.utenti_discord cu on cu.discord_id = s.id_capitano
                join fanta_torneo.utenti_discord gu on gu.discord_id = a.id_iscritto
                left join fanta_torneo.utenti_iscritti i on i.id_discord = a.id_iscritto
            ) fatti
            -- si vedono solo i fatti successivi all'ultimo "Avvia nuova offerta" dello streamer (configurazioni.inizio_log_asta)
            where quando > coalesce((select valore::timestamptz from fanta_torneo.configurazioni where chiave = 'inizio_log_asta'), '-infinity')
            order by quando desc, ordine
            limit 30
            """)).ToList();

    private static async Task<StatoPuntate> LeggiPuntateBase(NpgsqlConnection c)
    {
        var fase = await c.ExecuteScalarAsync<string?>("select valore from fanta_torneo.configurazioni where chiave = 'fase_flusso_asta'");
        // le puntate si vedono in 'fase di acquisto' e, a offerta chiusa, in 'acquisto fatto' (finché non si avvia una nuova offerta)
        // prima della chiamata non ci sono puntate, ma la stella indica il capitano di turno
        if (fase == "chiamata_giocatore") return new StatoPuntate(fase, null, 0, 0, [], (await TrovaTurno(c)).IdCapitano);
        if (fase != "fase di acquisto" && fase != "acquisto fatto") return new StatoPuntate(fase, null, 0, 0, []);

        var giocatore = await c.QueryFirstOrDefaultAsync<(string IdDiscord, string? Nome, int Elo, string IdChiamante)?>("""
            select h.id_utente as iddiscord, coalesce(i.nickname, u.discord_nome) as nome, coalesce(i.elo_calcolato, 0) as elo, h.id_chiamato_da as idchiamante
            from fanta_torneo.asta_giocatori_chiamati h
            join fanta_torneo.utenti_discord u on u.discord_id = h.id_utente
            left join fanta_torneo.utenti_iscritti i on i.id_discord = h.id_utente
            where h.data_inserimento > coalesce((select valore::timestamptz from fanta_torneo.configurazioni where chiave = 'inizio_log_asta'), '-infinity')
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
            offerte.GroupBy(o => o.IdCapitano).Select(g => new PuntateCapitano(g.Key, g.TakeLast(7).Select(o => o.Puntata).Order().ToList())).ToList(),
            giocatore.Value.IdChiamante,
            offerte.Count == 0 ? null : offerte.MaxBy(o => o.Puntata)!.IdCapitano);
    }

    // Come lo stato viene inviato al client (stessa forma per l'endpoint e per l'evento)
    private static object Proiezione(StatoPuntate p) => new
    {
        fase = p.Fase, giocatore = p.Giocatore, eloGiocatore = p.EloGiocatore, puntataMassima = p.PuntataMassima,
        idChiamante = p.IdChiamante, idMigliore = p.IdMigliore,
        scadenzaMs = p.ScadenzaMs, residuoMs = p.ResiduoMs, oraServerMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), // per il conto alla rovescia a video
        puntate = p.Puntate.Select(x => new { idCapitano = x.IdCapitano, valori = x.Valori }),
        log = (p.Log ?? []).Select(l => new { tipo = l.Tipo, capitano = l.Capitano, giocatore = l.Giocatore, valore = l.Valore, quando = l.Quando }),
    };

    // Avvisa tutti i client connessi: tipo "chiamata" (nuovo giocatore in acquisto) o "offerta"; con lo stato delle puntate già pronto.
    private Task Notifica(NpgsqlConnection c, string tipo) => Notifica(hub, c, tipo);

    internal static async Task Notifica(IHubContext<AstaHub> hub, NpgsqlConnection c, string tipo)
    {
        var p = await LeggiPuntate(c);
        await hub.Clients.All.SendAsync(AstaHub.EventoAggiornata, new
        {
            tipo,
            puntate = Proiezione(p),
        });
    }

    [HttpGet("/api/asta/puntate")]
    public async Task<IResult> Puntate()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);

        await using var c = await db.OpenConnectionAsync();
        var p = await LeggiPuntate(c);
        return Results.Ok(Proiezione(p));
    }

    private static Task<bool> EStreamer(NpgsqlConnection c, string idDiscord) =>
        c.ExecuteScalarAsync<bool>("""
            select exists(select 1 from fanta_torneo.utenti_ruoli ur
                          join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                          where ur.id_utente = @idDiscord and r.chiave_ruolo = 'streamer')
            """, new { idDiscord });

    // Lo streamer chiude l'offerta in corso: il giocatore va alla squadra del capitano con l'offerta più alta
    // (asta_giocatori_acquistati, al prezzo di quell'offerta), il costo si toglie al suo credito residuo, la chiamata passa a 'acquistato' e la fase a 'acquisto fatto'.
    [HttpPost("/api/asta/termina-offerta")]
    public async Task<IResult> TerminaOfferta()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();

        await using var c = await db.OpenConnectionAsync();
        if (!await EStreamer(c, DiscordId(User)!))
            return Results.Json(new { errore = "Solo lo streamer può terminare l'offerta." }, statusCode: 403);

        var errore = await ChiudiOfferta(c, soloSeScaduta: false);
        if (errore is { } e) return Results.Json(new { errore = e.Errore }, statusCode: e.Codice);
        timer.Annulla();
        await Notifica(c, "acquisto");
        return Results.Ok(new { ok = true });
    }

    // Chiude l'offerta in corso: il giocatore va alla squadra del miglior offerente, che paga; la chiamata passa a 'acquistato',
    // la fase a 'acquisto fatto' e il timer si spegne. Serve allo streamer ("Termina offerta") e alla scadenza del timer
    // (soloSeScaduta: si controlla, dopo il blocco, che nel frattempo nessuno abbia rilanciato). Restituisce l'errore o null.
    internal static async Task<(int Codice, string Errore)?> ChiudiOfferta(NpgsqlConnection c, bool soloSeScaduta)
    {
        await using var tx = await c.BeginTransactionAsync();
        await c.ExecuteAsync("select pg_advisory_xact_lock(727001)", transaction: tx); // stesso blocco delle offerte
        var fase = await c.ExecuteScalarAsync<string?>("select valore from fanta_torneo.configurazioni where chiave = 'fase_flusso_asta'", transaction: tx);
        var giocatore = fase == "fase di acquisto"
            ? await c.ExecuteScalarAsync<string?>("select id_utente from fanta_torneo.asta_giocatori_chiamati where stato = 'fase di acquisto' order by data_inserimento desc limit 1", transaction: tx)
            : null;
        if (giocatore is null)
            return (409, "Non c'è nessuna offerta da terminare.");
        if (soloSeScaduta)
        {
            var scaduta = await c.ExecuteScalarAsync<bool>(
                "select coalesce((select valore::timestamptz <= now() from fanta_torneo.configurazioni where chiave = 'scadenza_asta'), false)", transaction: tx);
            if (!scaduta) return (409, "Il timer non è ancora scaduto.");
        }

        var migliore = await c.QueryFirstOrDefaultAsync<(string Capitano, int Puntata)?>(
            "select id_chiamato_da as capitano, puntata from fanta_torneo.asta_offerte where id_utente = @giocatore order by puntata desc, id_incrementale limit 1",
            new { giocatore }, tx);
        if (migliore is null)
            return (409, "Il giocatore non ha nessuna offerta.");
        var idSquadra = await c.ExecuteScalarAsync<int?>("select id from fanta_torneo.squadre where id_capitano = @capitano", new { capitano = migliore.Value.Capitano }, tx);
        if (idSquadra is null)
            return (409, "Il capitano con l'offerta più alta non ha una squadra.");

        await c.ExecuteAsync("""
            insert into fanta_torneo.asta_giocatori_acquistati (id_squadra, id_iscritto, costo_acquisto, data_acquisto)
            values (@idSquadra, @giocatore, @costo, now())
            """, new { idSquadra, giocatore, costo = migliore.Value.Puntata }, tx);
        // il capitano paga: il costo si sottrae al suo credito residuo (le offerte non possono superarlo, quindi non va sotto zero)
        await c.ExecuteAsync(
            "update fanta_torneo.asta_portafoglio_utente set credito_residuo = credito_residuo - @costo where id_utente = @capitano",
            new { costo = migliore.Value.Puntata, capitano = migliore.Value.Capitano }, tx);
        await c.ExecuteAsync("update fanta_torneo.asta_giocatori_chiamati set stato = 'acquistato' where id_utente = @giocatore", new { giocatore }, tx);
        await c.ExecuteAsync("update fanta_torneo.configurazioni set valore = 'acquisto fatto' where chiave = 'fase_flusso_asta'", transaction: tx);
        await c.ExecuteAsync("delete from fanta_torneo.configurazioni where chiave in ('scadenza_asta', 'timer_residuo_ms')", transaction: tx);
        await tx.CommitAsync();
        return null;
    }

    public sealed record RichiestaEliminaAcquisto(int? IdAcquisto);

    // Lo streamer elimina un acquisto (di qualsiasi giocatore di qualsiasi squadra, capitani esclusi), solo a offerta terminata (fase 'acquisto fatto', prima di "Avvia nuova offerta"): il
    // giocatore torna disponibile (si cancellano le sue offerte e la sua chiamata, e l'acquisto) e il capitano riottiene i crediti.
    [HttpPost("/api/asta/elimina-acquisto")]
    public async Task<IResult> EliminaAcquisto(RichiestaEliminaAcquisto req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (req.IdAcquisto is not int idAcquisto) return Results.BadRequest(new { errore = "Acquisto non indicato." });

        await using var c = await db.OpenConnectionAsync();
        if (!await EStreamer(c, DiscordId(User)!))
            return Results.Json(new { errore = "Solo lo streamer può eliminare un acquisto." }, statusCode: 403);

        await using var tx = await c.BeginTransactionAsync();
        await c.ExecuteAsync("select pg_advisory_xact_lock(727001)", transaction: tx);
        var fase = await c.ExecuteScalarAsync<string?>("select valore from fanta_torneo.configurazioni where chiave = 'fase_flusso_asta'", transaction: tx);
        if (fase != "acquisto fatto")
            return Results.Json(new { errore = "Si può eliminare un acquisto solo a offerta terminata, prima di avviare una nuova offerta." }, statusCode: 409);

        var acquisto = await c.QueryFirstOrDefaultAsync<(int IdSquadra, string Giocatore, int Costo)?>(
            "select id_squadra as idsquadra, id_iscritto as giocatore, costo_acquisto as costo from fanta_torneo.asta_giocatori_acquistati where id = @idAcquisto",
            new { idAcquisto }, tx);
        if (acquisto is null)
            return Results.Json(new { errore = "Acquisto non trovato." }, statusCode: 404);
        // il capitano fa parte della squadra per definizione (a costo 0): non si elimina
        var eCapitano = await c.ExecuteScalarAsync<bool>(
            "select exists(select 1 from fanta_torneo.squadre where id = @idSquadra and id_capitano = @giocatore)",
            new { idSquadra = acquisto.Value.IdSquadra, giocatore = acquisto.Value.Giocatore }, tx);
        if (eCapitano)
            return Results.Json(new { errore = "Il capitano non si può eliminare dalla sua squadra." }, statusCode: 409);

        // crediti restituiti al capitano della squadra, che li aveva pagati
        await c.ExecuteAsync("""
            update fanta_torneo.asta_portafoglio_utente set credito_residuo = credito_residuo + @costo
            where id_utente = (select id_capitano from fanta_torneo.squadre where id = @idSquadra)
            """, new { costo = acquisto.Value.Costo, idSquadra = acquisto.Value.IdSquadra }, tx);
        // il giocatore torna come mai chiamato: via offerte, chiamata (se c'era: così cambia anche il turno) e acquisto
        var giocatore = acquisto.Value.Giocatore;
        await c.ExecuteAsync("delete from fanta_torneo.asta_offerte where id_utente = @giocatore", new { giocatore }, tx);
        await c.ExecuteAsync("delete from fanta_torneo.asta_giocatori_chiamati where id_utente = @giocatore", new { giocatore }, tx);
        await c.ExecuteAsync("delete from fanta_torneo.asta_giocatori_acquistati where id = @idAcquisto", new { idAcquisto }, tx);
        await tx.CommitAsync();
        await Notifica(c, "eliminato");
        return Results.Ok(new { ok = true });
    }

    // Lo streamer ferma il timer: si salva quanto tempo restava (timer_residuo_ms) e si toglie la scadenza; le offerte restano bloccate.
    [HttpPost("/api/asta/pausa-timer")]
    public async Task<IResult> PausaTimer()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();

        await using var c = await db.OpenConnectionAsync();
        if (!await EStreamer(c, DiscordId(User)!))
            return Results.Json(new { errore = "Solo lo streamer può mettere in pausa il timer." }, statusCode: 403);

        await using var tx = await c.BeginTransactionAsync();
        await c.ExecuteAsync("select pg_advisory_xact_lock(727001)", transaction: tx);
        var residuo = await c.ExecuteScalarAsync<long?>("""
            select greatest(0, (extract(epoch from valore::timestamptz) * 1000)::bigint - (extract(epoch from now()) * 1000)::bigint)
            from fanta_torneo.configurazioni where chiave = 'scadenza_asta'
            """, transaction: tx);
        if (residuo is null)
            return Results.Json(new { errore = "Il timer non è attivo." }, statusCode: 409);
        await c.ExecuteAsync("""
            insert into fanta_torneo.configurazioni (chiave, valore, descrizione)
            values ('timer_residuo_ms', @residuo::text, 'Timer dell''asta in pausa: millisecondi rimasti quando lo streamer lo ha fermato')
            on conflict (chiave) do update set valore = excluded.valore
            """, new { residuo }, tx);
        await c.ExecuteAsync("delete from fanta_torneo.configurazioni where chiave = 'scadenza_asta'", transaction: tx);
        await tx.CommitAsync();
        timer.Annulla();
        await Notifica(c, "timer");
        return Results.Ok(new { ok = true });
    }

    // Lo streamer riprende il timer: riparte da dove si era fermato.
    [HttpPost("/api/asta/riprendi-timer")]
    public async Task<IResult> RiprendiTimer()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();

        await using var c = await db.OpenConnectionAsync();
        if (!await EStreamer(c, DiscordId(User)!))
            return Results.Json(new { errore = "Solo lo streamer può riprendere il timer." }, statusCode: 403);

        await using var tx = await c.BeginTransactionAsync();
        await c.ExecuteAsync("select pg_advisory_xact_lock(727001)", transaction: tx);
        var residuo = await LeggiResiduoMs(c);
        if (residuo is null)
            return Results.Json(new { errore = "Il timer non è in pausa." }, statusCode: 409);
        var ms = await c.ExecuteScalarAsync<long>("""
            insert into fanta_torneo.configurazioni (chiave, valore, descrizione)
            values ('scadenza_asta', (now() + make_interval(secs => @secondi))::text, 'Quando scade il timer dell''offerta in corso (si imposta a ogni chiamata e offerta)')
            on conflict (chiave) do update set valore = excluded.valore
            returning (extract(epoch from valore::timestamptz) * 1000)::bigint
            """, new { secondi = residuo.Value / 1000.0 }, tx);
        await c.ExecuteAsync("delete from fanta_torneo.configurazioni where chiave = 'timer_residuo_ms'", transaction: tx);
        await tx.CommitAsync();
        timer.Pianifica(DateTimeOffset.FromUnixTimeMilliseconds(ms));
        await Notifica(c, "timer");
        return Results.Ok(new { ok = true });
    }

    // Lo streamer avvia una nuova offerta: le puntate si azzerano a video (restano nello storico) e la fase torna a
    // 'chiamata_giocatore', così chiama il capitano successivo.
    [HttpPost("/api/asta/nuova-offerta")]
    public async Task<IResult> NuovaOfferta()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();

        await using var c = await db.OpenConnectionAsync();
        if (!await EStreamer(c, DiscordId(User)!))
            return Results.Json(new { errore = "Solo lo streamer può avviare una nuova offerta." }, statusCode: 403);

        await using var tx = await c.BeginTransactionAsync();
        var cambiata = await c.ExecuteAsync("""
            update fanta_torneo.configurazioni set valore = 'chiamata_giocatore'
            where chiave = 'fase_flusso_asta' and valore = 'acquisto fatto'
            """, transaction: tx);
        if (cambiata == 0)
            return Results.Json(new { errore = "Si può avviare una nuova offerta solo dopo aver terminato quella in corso." }, statusCode: 409);
        // i messaggi dello stato dell'asta ripartono da zero: restano nel database, ma si mostrano solo quelli da questo momento in poi
        await c.ExecuteAsync("""
            insert into fanta_torneo.configurazioni (chiave, valore, descrizione)
            values ('inizio_log_asta', now()::text, 'Da quando si mostrano i messaggi dello stato dell''asta: si aggiorna ogni volta che lo streamer avvia una nuova offerta')
            on conflict (chiave) do update set valore = excluded.valore
            """, transaction: tx);
        await tx.CommitAsync();
        await Notifica(c, "nuova");
        return Results.Ok(new { ok = true });
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

        if (await LeggiResiduoMs(c) is not null)
            return Results.Json(new { errore = "Il timer è in pausa: non si può offrire finché lo streamer non lo riprende." }, statusCode: 409);

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
        var scadenza = await ImpostaScadenza(c, tx);
        await tx.CommitAsync();
        timer.Pianifica(scadenza);
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
            select s.id, s.nome, u.discord_nome as capitano, p.credito_residuo as credito, s.id_capitano as idcapitano, coalesce(o.ordine, 0) as ordine
            from fanta_torneo.squadre s
            left join fanta_torneo.utenti_discord u on u.discord_id = s.id_capitano
            left join fanta_torneo.asta_portafoglio_utente p on p.id_utente = s.id_capitano
            left join fanta_torneo.asta_ordine_capitani o on o.id_utente = s.id_capitano
            order by s.id
            """)).ToList();
        var giocatori = (await c.QueryAsync<RigaGiocatore>("""
            select a.id_squadra as idsquadra, a.id_iscritto as iddiscord, i.nickname as nome, i.elo_calcolato as elo, a.costo_acquisto as costo,
                   (a.id_iscritto = s.id_capitano) as capitano, a.id as idacquisto,
                   (a.id_iscritto is distinct from s.id_capitano) as eliminabile
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
            s.Ordine, // posizione del capitano nel flusso dell'asta (asta_ordine_capitani.ordine; 0 = non assegnata)
            s.Credito, // crediti rimasti: asta_portafoglio_utente.credito_residuo del capitano (null se non ha un portafoglio)
            Giocatori = giocatori[s.Id].Select(g => new { Nome = g.Nome ?? "", g.Elo, g.Costo, g.Capitano, g.IdAcquisto, g.Eliminabile, Tu = io is not null && g.IdDiscord == io }).ToList(),
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
