using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Npgsql;

namespace FantaAoe.Api;

public static class ApiTorneo
{
    private const int MaxRosa = 10;

    private sealed class IdElo { public int Id { get; set; } public int Elo { get; set; } }
    private sealed class OffertaPrec { public int SquadraId { get; set; } public int Importo { get; set; } }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string? DiscordId(ClaimsPrincipal u) => u.FindFirst("discord_id")?.Value;
    private static string NomeUtente(ClaimsPrincipal u) => u.FindFirst("nome")?.Value ?? "Utente";
    private static string? AvatarUtente(ClaimsPrincipal u) => u.FindFirst("avatar")?.Value;
    private static bool EAdmin(ClaimsPrincipal u) => u.Identity?.IsAuthenticated == true && u.IsInRole(Accesso.Admin);
    private static bool ECapitano(ClaimsPrincipal u) => u.Identity?.IsAuthenticated == true && (u.IsInRole(Accesso.Capitano) || u.IsInRole(Accesso.Admin));
    private static bool EAutenticato(ClaimsPrincipal u) => u.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(DiscordId(u));

    public static async Task RegistraUtenteDiscord(NpgsqlDataSource? db, ClaimsPrincipal u)
    {
        if (db is null || !EAutenticato(u)) return;
        var did = DiscordId(u)!;
        var nome = NomeUtente(u);
        var avatar = AvatarUtente(u);
        try
        {
            await using var c = await db.OpenConnectionAsync();
            await c.ExecuteAsync("""
                insert into fanta_torneo.utenti_discord (discord_id, discord_nome, discord_avatar, aggiornato_il)
                values (@did, @nome, @avatar, now())
                on conflict (discord_id) do update
                set discord_nome = excluded.discord_nome,
                    discord_avatar = excluded.discord_avatar,
                    aggiornato_il = now()
                """, new { did, nome, avatar });
        }
        catch
        {
            // non bloccare il login se il DB torneo non è temporaneamente disponibile
        }
    }

    public static void MappaApiTorneo(this WebApplication app, NpgsqlDataSource? db, AstaHub hub)
    {
        // =====================================================================
        // BLOCCO A: PROFILO GIOCATORE, COLLEGAMENTO DISCORD, ISCRITTI, SQUADRE
        // =====================================================================

        app.MapGet("/api/profilo", async (ClaimsPrincipal u) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAutenticato(u)) return Results.Unauthorized();
            var did = DiscordId(u)!;
            await RegistraUtenteDiscord(db, u);

            await using var c = await db.OpenConnectionAsync();
            var legame = await c.QuerySingleOrDefaultAsync<UtenteDiscord>("""
                select u.discord_id, u.discord_nome, u.discord_avatar, u.player_id,
                       g.nick as player_nick, u.collegato_da, u.verificato, u.aggiornato_il
                from fanta_torneo.utenti_discord u
                left join fanta.giocatori g on g.player_id = u.player_id
                where u.discord_id = @did
                """, new { did });

            GiocatoreDettaglio? giocatore = null;
            IEnumerable<CivStat> civ = [];
            if (legame?.PlayerId is int pid)
            {
                giocatore = await c.QuerySingleOrDefaultAsync<GiocatoreDettaglio>(
                    "select * from fanta.giocatori where player_id = @pid", new { pid });
                civ = await c.QueryAsync<CivStat>("""
                    select pg.civ, count(*) as partite, sum(pg.vinto) as vittorie
                    from fanta.partite_giocatori pg join fanta.partite p using (id_game)
                    where pg.player_id = @pid and p.valida and pg.civ is not null
                    group by pg.civ
                    order by count(*) desc, sum(pg.vinto) desc
                    limit 6
                    """, new { pid });
            }

            var iscrizione = await c.QuerySingleOrDefaultAsync<Iscritto>("""
                select i.id, i.player_id, i.discord_id, i.nick, trim(i.fascia) as fascia,
                       i.seeding, i.prezzo_base, i.squadra_id, s.nome as squadra_nome,
                       i.prezzo_asta, i.note,
                       g.elo_interno, g.elo_1v1, g.elo_tg, g.n_diti, g.winrate
                from fanta_torneo.iscritti i
                left join fanta_torneo.squadre s on s.id = i.squadra_id
                left join fanta.giocatori g on g.player_id = i.player_id
                where i.discord_id = @did or (@pid is not null and i.player_id = @pid)
                limit 1
                """, new { did, pid = legame?.PlayerId });

            return Results.Ok(new { legame, giocatore, civ, iscrizione });
        });

        app.MapPost("/api/profilo/collega", async (ClaimsPrincipal u, RichiestaCollega req) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAutenticato(u)) return Results.Unauthorized();
            var did = DiscordId(u)!;
            await RegistraUtenteDiscord(db, u);

            await using var c = await db.OpenConnectionAsync();
            if (req.PlayerId is int pid)
            {
                var esiste = await c.ExecuteScalarAsync<bool>(
                    "select exists(select 1 from fanta.giocatori where player_id = @pid)", new { pid });
                if (!esiste) return Results.BadRequest(new { errore = "Giocatore non trovato in classifica." });
            }

            var verificato = EAdmin(u);
            await c.ExecuteAsync("""
                update fanta_torneo.utenti_discord
                set player_id = @pid,
                    collegato_da = @chi,
                    verificato = @verificato,
                    aggiornato_il = now()
                where discord_id = @did
                """, new { pid = req.PlayerId, chi = verificato ? "admin" : "utente", verificato, did });

            // se l'iscritto con quel player_id esiste e non ha discord_id, lo aggancia
            if (req.PlayerId is int pid2)
            {
                await c.ExecuteAsync("""
                    update fanta_torneo.iscritti
                    set discord_id = @did
                    where player_id = @pid2 and discord_id is null
                    """, new { did, pid2 });
            }

            return Results.Ok(new { ok = true });
        });

        // Lista iscritti (pubblica o autenticata)
        app.MapGet("/api/torneo/iscritti", async () =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            await using var c = await db.OpenConnectionAsync();
            var righe = await c.QueryAsync<Iscritto>("""
                select i.id, i.player_id, i.discord_id, i.nick, trim(i.fascia) as fascia,
                       i.seeding, i.prezzo_base, i.squadra_id, s.nome as squadra_nome,
                       i.prezzo_asta, i.note,
                       g.elo_interno, g.elo_1v1, g.elo_tg, g.n_diti, g.winrate
                from fanta_torneo.iscritti i
                left join fanta_torneo.squadre s on s.id = i.squadra_id
                left join fanta.giocatori g on g.player_id = i.player_id
                order by i.fascia asc, i.seeding asc nulls last, g.elo_1v1 desc nulls last, i.nick asc
                """);
            return Results.Ok(righe);
        });

        // Admin: aggiungi/aggiorna iscritto
        app.MapPost("/api/admin/iscritti", async (ClaimsPrincipal u, RichiestaIscritto req) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();

            var nick = req.Nick?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(nick)) return Results.BadRequest(new { errore = "Il nick è obbligatorio." });
            var fascia = (req.Fascia ?? "C").Trim().ToUpperInvariant();
            if (fascia is not ("A" or "B" or "C")) return Results.BadRequest(new { errore = "Fascia non valida (usa A, B o C)." });
            var prezzoBase = Math.Max(1, req.PrezzoBase);
            var did = string.IsNullOrWhiteSpace(req.DiscordId) ? null : req.DiscordId.Trim();

            await using var c = await db.OpenConnectionAsync();
            if (did is null && req.PlayerId is int pid)
            {
                did = await c.ExecuteScalarAsync<string?>(
                    "select discord_id from fanta_torneo.utenti_discord where player_id = @pid limit 1", new { pid });
            }

            if (req.Id is int id)
            {
                await c.ExecuteAsync("""
                    update fanta_torneo.iscritti
                    set player_id = @PlayerId, discord_id = @did, nick = @nick,
                        fascia = @fascia, seeding = @Seeding, prezzo_base = @prezzoBase, note = @Note
                    where id = @id
                    """, new { id, req.PlayerId, did, nick, fascia, req.Seeding, prezzoBase, req.Note });
                return Results.Ok(new { id });
            }
            else
            {
                var nuovoId = await c.ExecuteScalarAsync<int>("""
                    insert into fanta_torneo.iscritti (player_id, discord_id, nick, fascia, seeding, prezzo_base, note)
                    values (@PlayerId, @did, @nick, @fascia, @Seeding, @prezzoBase, @Note)
                    on conflict (player_id) do update
                    set nick = excluded.nick, fascia = excluded.fascia, seeding = excluded.seeding,
                        prezzo_base = excluded.prezzo_base, note = excluded.note,
                        discord_id = coalesce(excluded.discord_id, fanta_torneo.iscritti.discord_id)
                    returning id
                    """, new { req.PlayerId, did, nick, fascia, req.Seeding, prezzoBase, req.Note });
                return Results.Ok(new { id = nuovoId });
            }
        });

        // Admin: auto-seeding e ripartizione fasce A (30%), B (30%), C (40%) per ELO 1v1 decrescente
        app.MapPost("/api/admin/iscritti/auto-seeding", async (ClaimsPrincipal u) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();

            await using var c = await db.OpenConnectionAsync();
            var lista = (await c.QueryAsync<IdElo>("""
                select i.id, coalesce(g.elo_1v1, g.elo_interno, 0) as elo
                from fanta_torneo.iscritti i
                left join fanta.giocatori g on g.player_id = i.player_id
                order by coalesce(g.elo_1v1, g.elo_interno, 0) desc, i.nick asc
                """)).ToList();

            var tot = lista.Count;
            if (tot == 0) return Results.Ok(new { aggiornati = 0 });

            var taglioA = (int)Math.Round(tot * 0.3);
            var taglioB = (int)Math.Round(tot * 0.6);

            for (var idx = 0; idx < tot; idx++)
            {
                var fascia = idx < taglioA ? "A" : idx < taglioB ? "B" : "C";
                await c.ExecuteAsync(
                    "update fanta_torneo.iscritti set seeding = @seed, fascia = @fascia where id = @id",
                    new { seed = idx + 1, fascia, id = lista[idx].Id });
            }
            return Results.Ok(new { aggiornati = tot });
        });

        app.MapDelete("/api/admin/iscritti/{id:int}", async (ClaimsPrincipal u, int id) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();
            await using var c = await db.OpenConnectionAsync();
            await c.ExecuteAsync("delete from fanta_torneo.iscritti where id = @id", new { id });
            return Results.Ok(new { ok = true });
        });

        // Admin: utenti Discord visti al login e verifica collegamento
        app.MapGet("/api/admin/utenti-discord", async (ClaimsPrincipal u) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();
            await using var c = await db.OpenConnectionAsync();
            var righe = await c.QueryAsync<UtenteDiscord>("""
                select u.discord_id, u.discord_nome, u.discord_avatar, u.player_id,
                       g.nick as player_nick, u.collegato_da, u.verificato, u.aggiornato_il
                from fanta_torneo.utenti_discord u
                left join fanta.giocatori g on g.player_id = u.player_id
                order by u.aggiornato_il desc
                """);
            return Results.Ok(righe);
        });

        app.MapPost("/api/admin/utenti-discord/{discordId}/collega", async (ClaimsPrincipal u, string discordId, RichiestaCollega req) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();
            await using var c = await db.OpenConnectionAsync();
            await c.ExecuteAsync("""
                update fanta_torneo.utenti_discord
                set player_id = @pid,
                    collegato_da = 'admin',
                    verificato = @ver,
                    aggiornato_il = now()
                where discord_id = @discordId
                """, new { pid = req.PlayerId, ver = req.Verificato ?? true, discordId });

            if (req.PlayerId is int pid)
            {
                await c.ExecuteAsync("""
                    update fanta_torneo.iscritti
                    set discord_id = @discordId
                    where player_id = @pid
                    """, new { discordId, pid });
            }
            return Results.Ok(new { ok = true });
        });

        // Squadre (pubbliche + gestione admin)
        app.MapGet("/api/torneo/squadre", async () =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            await using var c = await db.OpenConnectionAsync();
            var squadre = await c.QueryAsync<Squadra>("""
                select s.id, s.nome, s.capitano_discord_id, s.capitano_nome,
                       s.berlu_iniziali, s.berlu_residui, s.ordine_chiamata, s.canale_discord_url,
                       (select count(*) from fanta_torneo.iscritti i where i.squadra_id = s.id)::int as n_giocatori
                from fanta_torneo.squadre s
                order by s.ordine_chiamata asc, s.id asc
                """);
            return Results.Ok(squadre);
        });

        app.MapPost("/api/admin/squadre", async (ClaimsPrincipal u, RichiestaSquadra req) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();

            var nome = req.Nome?.Trim() ?? "";
            var capNome = req.CapitanoNome?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(capNome))
                return Results.BadRequest(new { errore = "Nome squadra e nome capitano sono obbligatori." });

            var berlu = Math.Max(1, req.BerluIniziali);
            var ordine = Math.Max(1, req.OrdineChiamata);
            var capDid = string.IsNullOrWhiteSpace(req.CapitanoDiscordId) ? null : req.CapitanoDiscordId.Trim();
            var canale = string.IsNullOrWhiteSpace(req.CanaleDiscordUrl) ? null : req.CanaleDiscordUrl.Trim();

            await using var c = await db.OpenConnectionAsync();
            if (req.Id is int id)
            {
                await c.ExecuteAsync("""
                    update fanta_torneo.squadre
                    set nome = @nome, capitano_nome = @capNome, capitano_discord_id = @capDid,
                        berlu_iniziali = @berlu, ordine_chiamata = @ordine, canale_discord_url = @canale
                    where id = @id
                    """, new { id, nome, capNome, capDid, berlu, ordine, canale });
                await NotificaAsta(db, hub);
                return Results.Ok(new { id });
            }
            else
            {
                var nuovoId = await c.ExecuteScalarAsync<int>("""
                    insert into fanta_torneo.squadre (nome, capitano_nome, capitano_discord_id, berlu_iniziali, berlu_residui, ordine_chiamata, canale_discord_url)
                    values (@nome, @capNome, @capDid, @berlu, @berlu, @ordine, @canale)
                    returning id
                    """, new { nome, capNome, capDid, berlu, ordine, canale });
                await NotificaAsta(db, hub);
                return Results.Ok(new { id = nuovoId });
            }
        });

        app.MapDelete("/api/admin/squadre/{id:int}", async (ClaimsPrincipal u, int id) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();
            await using var c = await db.OpenConnectionAsync();
            await c.ExecuteAsync("delete from fanta_torneo.squadre where id = @id", new { id });
            await NotificaAsta(db, hub);
            return Results.Ok(new { ok = true });
        });

        // =====================================================================
        // BLOCCO B: DISPONIBILITÀ E TICKET
        // =====================================================================

        app.MapGet("/api/disponibilita/mie", async (ClaimsPrincipal u) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAutenticato(u)) return Results.Unauthorized();
            var did = DiscordId(u)!;
            await using var c = await db.OpenConnectionAsync();
            var righe = await c.QueryAsync<DisponibilitaRiga>("""
                select id, discord_id, data, stato, fascia_oraria, note, aggiornata_il
                from fanta_torneo.disponibilita
                where discord_id = @did and data >= current_date - 1 and data <= current_date + 28
                order by data asc
                """, new { did });
            return Results.Ok(righe);
        });

        app.MapPut("/api/disponibilita", async (ClaimsPrincipal u, RichiestaDisponibilita req) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAutenticato(u)) return Results.Unauthorized();
            var did = DiscordId(u)!;

            if (!DateOnly.TryParse(req.Data, out var dataGiorno))
                return Results.BadRequest(new { errore = "Data non valida (usa YYYY-MM-DD)." });
            var stato = (req.Stato ?? "").Trim().ToLowerInvariant();
            if (stato is not ("disponibile" or "forse" or "indisponibile"))
                return Results.BadRequest(new { errore = "Stato non valido." });

            await using var c = await db.OpenConnectionAsync();
            await c.ExecuteAsync("""
                insert into fanta_torneo.disponibilita (discord_id, data, stato, fascia_oraria, note, aggiornata_il)
                values (@did, @data, @stato, @fascia, @note, now())
                on conflict (discord_id, data) do update
                set stato = excluded.stato,
                    fascia_oraria = excluded.fascia_oraria,
                    note = excluded.note,
                    aggiornata_il = now()
                """, new
            {
                did,
                data = dataGiorno.ToDateTime(TimeOnly.MinValue),
                stato,
                fascia = string.IsNullOrWhiteSpace(req.FasciaOraria) ? null : req.FasciaOraria.Trim(),
                note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            });
            return Results.Ok(new { ok = true });
        });

        // Vista Capitano: la propria squadra (o una squadra scelta dall'admin), rosa e disponibilità dei prossimi 14 giorni
        app.MapGet("/api/capitano/squadra", async (ClaimsPrincipal u, int? squadraId) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!ECapitano(u)) return Results.Forbid();
            var did = DiscordId(u)!;

            await using var c = await db.OpenConnectionAsync();
            Squadra? squadra = null;
            if (squadraId is int sid && EAdmin(u))
            {
                squadra = await c.QuerySingleOrDefaultAsync<Squadra>("""
                    select s.id, s.nome, s.capitano_discord_id, s.capitano_nome,
                           s.berlu_iniziali, s.berlu_residui, s.ordine_chiamata, s.canale_discord_url,
                           (select count(*) from fanta_torneo.iscritti i where i.squadra_id = s.id)::int as n_giocatori
                    from fanta_torneo.squadre s where s.id = @sid
                    """, new { sid });
            }
            else
            {
                squadra = await c.QuerySingleOrDefaultAsync<Squadra>("""
                    select s.id, s.nome, s.capitano_discord_id, s.capitano_nome,
                           s.berlu_iniziali, s.berlu_residui, s.ordine_chiamata, s.canale_discord_url,
                           (select count(*) from fanta_torneo.iscritti i where i.squadra_id = s.id)::int as n_giocatori
                    from fanta_torneo.squadre s where s.capitano_discord_id = @did limit 1
                    """, new { did });
            }

            if (squadra is null) return Results.Ok(new { squadra = (Squadra?)null, rosa = Array.Empty<Iscritto>(), disponibilita = Array.Empty<DisponibilitaRiga>() });

            var rosa = (await c.QueryAsync<Iscritto>("""
                select i.id, i.player_id, i.discord_id, i.nick, trim(i.fascia) as fascia,
                       i.seeding, i.prezzo_base, i.squadra_id, s.nome as squadra_nome,
                       i.prezzo_asta, i.note,
                       g.elo_interno, g.elo_1v1, g.elo_tg, g.n_diti, g.winrate
                from fanta_torneo.iscritti i
                left join fanta_torneo.squadre s on s.id = i.squadra_id
                left join fanta.giocatori g on g.player_id = i.player_id
                where i.squadra_id = @sid
                order by i.fascia asc, i.seeding asc nulls last, i.nick asc
                """, new { sid = squadra.Id })).ToList();

            var dids = rosa.Where(r => !string.IsNullOrEmpty(r.DiscordId)).Select(r => r.DiscordId!).Distinct().ToArray();
            IEnumerable<DisponibilitaRiga> disp = [];
            if (dids.Length > 0)
            {
                disp = await c.QueryAsync<DisponibilitaRiga>("""
                    select id, discord_id, data, stato, fascia_oraria, note, aggiornata_il
                    from fanta_torneo.disponibilita
                    where discord_id = any(@dids) and data >= current_date and data <= current_date + 14
                    order by data asc
                    """, new { dids });
            }

            return Results.Ok(new { squadra, rosa, disponibilita = disp });
        });

        // Ticket: lista (tutti per admin, propri per giocatore)
        app.MapGet("/api/ticket", async (ClaimsPrincipal u, bool? tutti) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAutenticato(u)) return Results.Unauthorized();
            var did = DiscordId(u)!;
            var vediTutti = (tutti ?? false) && EAdmin(u);

            await using var c = await db.OpenConnectionAsync();
            var righe = vediTutti
                ? await c.QueryAsync<TicketRiga>("select * from fanta_torneo.ticket order by case stato when 'aperto' then 1 when 'in_corso' then 2 else 3 end, creato_il desc limit 100")
                : await c.QueryAsync<TicketRiga>("select * from fanta_torneo.ticket where autore_discord_id = @did order by creato_il desc limit 50", new { did });

            return Results.Ok(righe);
        });

        app.MapPost("/api/ticket", async (ClaimsPrincipal u, RichiestaTicket req) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAutenticato(u)) return Results.Unauthorized();

            var tipo = (req.Tipo ?? "segnalazione").Trim().ToLowerInvariant();
            if (tipo is not ("restart" or "segnalazione" or "infrazione" or "altro"))
                return Results.BadRequest(new { errore = "Tipo di ticket non valido." });

            var titolo = req.Titolo?.Trim() ?? "";
            var desc = req.Descrizione?.Trim() ?? "";
            if (titolo.Length < 3 || desc.Length < 5)
                return Results.BadRequest(new { errore = "Inserisci un titolo e una breve descrizione." });

            await using var c = await db.OpenConnectionAsync();
            var id = await c.ExecuteScalarAsync<int>("""
                insert into fanta_torneo.ticket (autore_discord_id, autore_nome, tipo, titolo, descrizione)
                values (@did, @nome, @tipo, @titolo, @desc)
                returning id
                """, new { did = DiscordId(u)!, nome = NomeUtente(u), tipo, titolo, desc });

            return Results.Ok(new { id });
        });

        app.MapPatch("/api/admin/ticket/{id:int}", async (ClaimsPrincipal u, int id, RichiestaGestisciTicket req) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();

            var stato = (req.Stato ?? "").Trim().ToLowerInvariant();
            if (stato is not ("aperto" or "in_corso" or "risolto" or "respinto"))
                return Results.BadRequest(new { errore = "Stato non valido." });

            await using var c = await db.OpenConnectionAsync();
            await c.ExecuteAsync("""
                update fanta_torneo.ticket
                set stato = @stato,
                    risposta_admin = @risposta,
                    gestito_da = @admin,
                    aggiornato_il = now()
                where id = @id
                """, new { id, stato, risposta = string.IsNullOrWhiteSpace(req.RispostaAdmin) ? null : req.RispostaAdmin.Trim(), admin = NomeUtente(u) });

            return Results.Ok(new { ok = true });
        });

        // =====================================================================
        // BLOCCO C: ASTA DAL VIVO + OVERLAY OBS (SSE)
        // =====================================================================

        app.MapGet("/api/asta/stato", async () =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            var payload = await LeggiStatoAsta(db);
            return Results.Ok(payload);
        });

        app.MapGet("/api/asta/stream", async (HttpContext ctx) =>
        {
            if (db is null) { ctx.Response.StatusCode = 503; return; }

            ctx.Response.Headers.ContentType = "text/event-stream; charset=utf-8";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            // Invia subito lo stato iniziale
            var iniziale = JsonSerializer.Serialize(await LeggiStatoAsta(db), JsonOpts);
            await ctx.Response.WriteAsync($"data: {iniziale}\n\n", ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);

            var (clientId, reader) = hub.Iscriviti();
            try
            {
                using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(25));
                var readTask = reader.ReadAsync(ctx.RequestAborted).AsTask();
                var beatTask = heartbeat.WaitForNextTickAsync(ctx.RequestAborted).AsTask();

                while (!ctx.RequestAborted.IsCancellationRequested)
                {
                    var done = await Task.WhenAny(readTask, beatTask);
                    if (done == readTask)
                    {
                        var msg = await readTask;
                        await ctx.Response.WriteAsync($"data: {msg}\n\n", ctx.RequestAborted);
                        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                        readTask = reader.ReadAsync(ctx.RequestAborted).AsTask();
                    }
                    else
                    {
                        await beatTask;
                        await ctx.Response.WriteAsync(": ping\n\n", ctx.RequestAborted);
                        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                        beatTask = heartbeat.WaitForNextTickAsync(ctx.RequestAborted).AsTask();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // disconnessione normale del browser / OBS
            }
            finally
            {
                hub.Disiscriviti(clientId);
            }
        });

        app.MapPost("/api/admin/asta/chiama", async (ClaimsPrincipal u, RichiestaChiamaAsta req) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();

            await using var c = await db.OpenConnectionAsync();
            var iscritto = await c.QuerySingleOrDefaultAsync<Iscritto>(
                "select id, nick, squadra_id, prezzo_base from fanta_torneo.iscritti where id = @id",
                new { id = req.IscrittoId });
            if (iscritto is null) return Results.BadRequest(new { errore = "Giocatore iscritto non trovato." });
            if (iscritto.SquadraId is not null) return Results.BadRequest(new { errore = "Questo giocatore è già stato assegnato a una squadra." });

            await c.ExecuteAsync("""
                update fanta_torneo.asta_stato
                set stato = 'in_corso',
                    iscritto_id = @id,
                    offerta_attuale = 0,
                    squadra_migliore_id = null,
                    messaggio = null,
                    aggiornato_il = now()
                where id = 1
                """, new { id = req.IscrittoId });

            await NotificaAsta(db, hub);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/admin/asta/stato", async (ClaimsPrincipal u, RichiestaStatoAsta req) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();

            var stato = (req.Stato ?? "").Trim().ToLowerInvariant();
            if (stato is not ("attesa" or "in_corso" or "pausa" or "chiusa"))
                return Results.BadRequest(new { errore = "Stato asta non valido." });

            await using var c = await db.OpenConnectionAsync();
            await c.ExecuteAsync("""
                update fanta_torneo.asta_stato
                set stato = @stato,
                    messaggio = @msg,
                    turno_squadra_id = coalesce(@turno, turno_squadra_id),
                    aggiornato_il = now()
                where id = 1
                """, new
            {
                stato,
                msg = string.IsNullOrWhiteSpace(req.Messaggio) ? null : req.Messaggio.Trim(),
                turno = req.TurnoSquadraId,
            });

            await NotificaAsta(db, hub);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/asta/offerta", async (ClaimsPrincipal u, RichiestaOffertaAsta req) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!ECapitano(u)) return Results.Forbid();

            var did = DiscordId(u)!;
            await using var c = await db.OpenConnectionAsync();
            await using var tx = await c.BeginTransactionAsync();

            var asta = await c.QuerySingleAsync<AstaStatoRiga>(
                "select * from fanta_torneo.asta_stato where id = 1 for update", transaction: tx);
            if (asta.Stato != "in_corso" || asta.IscrittoId is null)
                return Results.BadRequest(new { errore = "L'asta non è in corso su nessun giocatore." });

            Squadra? squadra;
            if (req.SquadraId is int sid && EAdmin(u))
            {
                squadra = await c.QuerySingleOrDefaultAsync<Squadra>("""
                    select s.id, s.nome, s.capitano_discord_id, s.capitano_nome,
                           s.berlu_iniziali, s.berlu_residui, s.ordine_chiamata, s.canale_discord_url,
                           (select count(*) from fanta_torneo.iscritti i where i.squadra_id = s.id)::int as n_giocatori
                    from fanta_torneo.squadre s where s.id = @sid
                    """, new { sid }, tx);
            }
            else
            {
                squadra = await c.QuerySingleOrDefaultAsync<Squadra>("""
                    select s.id, s.nome, s.capitano_discord_id, s.capitano_nome,
                           s.berlu_iniziali, s.berlu_residui, s.ordine_chiamata, s.canale_discord_url,
                           (select count(*) from fanta_torneo.iscritti i where i.squadra_id = s.id)::int as n_giocatori
                    from fanta_torneo.squadre s where s.capitano_discord_id = @did limit 1
                    """, new { did }, tx);
            }

            if (squadra is null)
                return Results.BadRequest(new { errore = "Nessuna squadra associata al tuo account capitano." });
            if (squadra.NGiocatori >= MaxRosa)
                return Results.BadRequest(new { errore = $"La squadra {squadra.Nome} ha già completato la rosa ({MaxRosa} giocatori)." });
            if (asta.SquadraMiglioreId == squadra.Id)
                return Results.BadRequest(new { errore = "Sei già in vantaggio su questo giocatore." });

            var prezzoBase = await c.ExecuteScalarAsync<int>(
                "select prezzo_base from fanta_torneo.iscritti where id = @id",
                new { id = asta.IscrittoId.Value }, tx);

            var minimo = asta.OffertaAttuale == 0 ? Math.Max(1, prezzoBase) : asta.OffertaAttuale + 1;
            var postiDopo = Math.Max(0, MaxRosa - squadra.NGiocatori - 1);
            var massimo = squadra.BerluResidui - postiDopo;

            if (req.Importo < minimo)
                return Results.BadRequest(new { errore = $"Offerta minima: {minimo} Berlu." });
            if (req.Importo > massimo)
                return Results.BadRequest(new { errore = $"Offerta massima consentita per {squadra.Nome}: {massimo} Berlu (devi tenere 1 Berlu per ciascuno dei {postiDopo} posti vuoti)." });

            await c.ExecuteAsync("""
                insert into fanta_torneo.asta_offerte (iscritto_id, squadra_id, importo, creata_da)
                values (@iscrittoId, @squadraId, @importo, @chi)
                """, new { iscrittoId = asta.IscrittoId.Value, squadraId = squadra.Id, importo = req.Importo, chi = NomeUtente(u) }, tx);

            await c.ExecuteAsync("""
                update fanta_torneo.asta_stato
                set offerta_attuale = @importo,
                    squadra_migliore_id = @squadraId,
                    aggiornato_il = now()
                where id = 1
                """, new { importo = req.Importo, squadraId = squadra.Id }, tx);

            await tx.CommitAsync();
            await NotificaAsta(db, hub);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/admin/asta/annulla-offerta", async (ClaimsPrincipal u) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();

            await using var c = await db.OpenConnectionAsync();
            await using var tx = await c.BeginTransactionAsync();

            var asta = await c.QuerySingleAsync<AstaStatoRiga>(
                "select * from fanta_torneo.asta_stato where id = 1 for update", transaction: tx);
            if (asta.IscrittoId is null)
                return Results.BadRequest(new { errore = "Nessun giocatore attualmente all'asta." });

            var ultimaId = await c.ExecuteScalarAsync<int?>("""
                select id from fanta_torneo.asta_offerte
                where iscritto_id = @iid and not annullata
                order by id desc limit 1
                """, new { iid = asta.IscrittoId.Value }, tx);

            if (ultimaId is null)
                return Results.BadRequest(new { errore = "Nessuna offerta da annullare." });

            await c.ExecuteAsync("update fanta_torneo.asta_offerte set annullata = true where id = @id", new { id = ultimaId.Value }, tx);

            var precedente = await c.QuerySingleOrDefaultAsync<OffertaPrec>("""
                select squadra_id, importo from fanta_torneo.asta_offerte
                where iscritto_id = @iid and not annullata
                order by id desc limit 1
                """, new { iid = asta.IscrittoId.Value }, tx);

            await c.ExecuteAsync("""
                update fanta_torneo.asta_stato
                set offerta_attuale = @imp,
                    squadra_migliore_id = @sid,
                    aggiornato_il = now()
                where id = 1
                """, new { imp = precedente?.Importo ?? 0, sid = precedente?.SquadraId }, tx);

            await tx.CommitAsync();
            await NotificaAsta(db, hub);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/admin/asta/aggiudica", async (ClaimsPrincipal u) =>
        {
            if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
            if (!EAdmin(u)) return Results.Forbid();

            await using var c = await db.OpenConnectionAsync();
            await using var tx = await c.BeginTransactionAsync();

            var asta = await c.QuerySingleAsync<AstaStatoRiga>(
                "select * from fanta_torneo.asta_stato where id = 1 for update", transaction: tx);
            if (asta.IscrittoId is null || asta.SquadraMiglioreId is null || asta.OffertaAttuale <= 0)
                return Results.BadRequest(new { errore = "Serve almeno un'offerta valida per aggiudicare il giocatore." });

            await c.ExecuteAsync("""
                update fanta_torneo.iscritti
                set squadra_id = @sid, prezzo_asta = @prezzo
                where id = @iid
                """, new { sid = asta.SquadraMiglioreId.Value, prezzo = asta.OffertaAttuale, iid = asta.IscrittoId.Value }, tx);

            await c.ExecuteAsync("""
                update fanta_torneo.squadre
                set berlu_residui = berlu_residui - @prezzo
                where id = @sid
                """, new { prezzo = asta.OffertaAttuale, sid = asta.SquadraMiglioreId.Value }, tx);

            // Calcola il prossimo turno a serpente
            var squadre = (await c.QueryAsync<int>(
                "select id from fanta_torneo.squadre order by ordine_chiamata asc, id asc", transaction: tx)).ToList();
            int? prossimoTurno = asta.TurnoSquadraId;
            var prossimoRound = asta.RoundNumero;
            if (squadre.Count > 0)
            {
                var ordine = (asta.RoundNumero % 2 == 1) ? squadre : squadre.AsEnumerable().Reverse().ToList();
                var pos = asta.TurnoSquadraId is int tid ? ordine.IndexOf(tid) : -1;
                if (pos < 0)
                {
                    prossimoTurno = ordine[0];
                }
                else if (pos + 1 < ordine.Count)
                {
                    prossimoTurno = ordine[pos + 1];
                }
                else
                {
                    prossimoRound = asta.RoundNumero + 1;
                    var nuovoOrdine = (prossimoRound % 2 == 1) ? squadre : squadre.AsEnumerable().Reverse().ToList();
                    prossimoTurno = nuovoOrdine[0];
                }
            }

            await c.ExecuteAsync("""
                update fanta_torneo.asta_stato
                set stato = 'attesa',
                    iscritto_id = null,
                    offerta_attuale = 0,
                    squadra_migliore_id = null,
                    turno_squadra_id = @prossimoTurno,
                    round_numero = @prossimoRound,
                    messaggio = null,
                    aggiornato_il = now()
                where id = 1
                """, new { prossimoTurno, prossimoRound }, tx);

            await tx.CommitAsync();
            await NotificaAsta(db, hub);
            return Results.Ok(new { ok = true });
        });
    }

    private static async Task<object> LeggiStatoAsta(NpgsqlDataSource db)
    {
        await using var c = await db.OpenConnectionAsync();
        var stato = await c.QuerySingleAsync<AstaStatoRiga>("select * from fanta_torneo.asta_stato where id = 1");

        Iscritto? corrente = null;
        IEnumerable<AstaOffertaRiga> offerte = [];
        if (stato.IscrittoId is int iid)
        {
            corrente = await c.QuerySingleOrDefaultAsync<Iscritto>("""
                select i.id, i.player_id, i.discord_id, i.nick, trim(i.fascia) as fascia,
                       i.seeding, i.prezzo_base, i.squadra_id, s.nome as squadra_nome,
                       i.prezzo_asta, i.note,
                       g.elo_interno, g.elo_1v1, g.elo_tg, g.n_diti, g.winrate
                from fanta_torneo.iscritti i
                left join fanta_torneo.squadre s on s.id = i.squadra_id
                left join fanta.giocatori g on g.player_id = i.player_id
                where i.id = @iid
                """, new { iid });

            offerte = await c.QueryAsync<AstaOffertaRiga>("""
                select o.id, o.iscritto_id, o.squadra_id, s.nome as squadra_nome,
                       o.importo, o.annullata, o.creata_da, o.creata_il
                from fanta_torneo.asta_offerte o
                join fanta_torneo.squadre s on s.id = o.squadra_id
                where o.iscritto_id = @iid and not o.annullata
                order by o.id desc
                limit 10
                """, new { iid });
        }

        var squadre = await c.QueryAsync<Squadra>("""
            select s.id, s.nome, s.capitano_discord_id, s.capitano_nome,
                   s.berlu_iniziali, s.berlu_residui, s.ordine_chiamata, s.canale_discord_url,
                   (select count(*) from fanta_torneo.iscritti i where i.squadra_id = s.id)::int as n_giocatori
            from fanta_torneo.squadre s
            order by s.ordine_chiamata asc, s.id asc
            """);

        var disponibili = await c.QueryAsync<Iscritto>("""
            select i.id, i.player_id, i.discord_id, i.nick, trim(i.fascia) as fascia,
                   i.seeding, i.prezzo_base, i.squadra_id, null::text as squadra_nome,
                   i.prezzo_asta, i.note,
                   g.elo_interno, g.elo_1v1, g.elo_tg, g.n_diti, g.winrate
            from fanta_torneo.iscritti i
            left join fanta.giocatori g on g.player_id = i.player_id
            where i.squadra_id is null
            order by i.fascia asc, i.seeding asc nulls last, i.nick asc
            """);

        return new
        {
            stato = stato.Stato,
            iscrittoId = stato.IscrittoId,
            offertaAttuale = stato.OffertaAttuale,
            squadraMiglioreId = stato.SquadraMiglioreId,
            turnoSquadraId = stato.TurnoSquadraId,
            roundNumero = stato.RoundNumero,
            messaggio = stato.Messaggio,
            aggiornatoIl = stato.AggiornatoIl,
            maxRosa = MaxRosa,
            giocatore = corrente,
            offerte,
            squadre,
            disponibili,
        };
    }

    private static async Task NotificaAsta(NpgsqlDataSource db, AstaHub hub)
    {
        var payload = await LeggiStatoAsta(db);
        hub.Notifica(JsonSerializer.Serialize(payload, JsonOpts));
    }
}
