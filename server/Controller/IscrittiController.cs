using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Npgsql;
using static FantaAoe.Api.TorneoUtil;

namespace FantaAoe.Api.Controllers;

[ApiController]
public class IscrittiController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public IscrittiController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

        // Lista iscritti (pubblica o autenticata)
    [HttpGet("/api/torneo/iscritti")]
    public async Task<IResult> Elenco()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync<Iscritto>("""
            select i.id, i.player_id, i.discord_id, i.nick, trim(i.fascia) as fascia,
                   i.seeding, i.prezzo_base, i.squadra_id, s.nome as squadra_nome,
                   i.prezzo_asta, i.stato, i.note,
                   g.elo_interno, g.elo_1v1, g.elo_tg, g.n_diti, g.winrate
            from fanta_torneo.iscritti i
            left join fanta_torneo.squadre s on s.id = i.squadra_id
            left join fanta.giocatori g on g.player_id = i.player_id
            order by case i.stato when 'in_attesa' then 0 when 'approvato' then 1 else 2 end,
                     i.fascia asc, i.seeding asc nulls last, g.elo_1v1 desc nulls last, i.nick asc
            """);
        return Results.Ok(righe);
    }

        // Admin: aggiungi/aggiorna iscritto
    [HttpPost("/api/admin/iscritti")]
    public async Task<IResult> Aggiungi(RichiestaIscritto req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAdmin(User)) return Results.Forbid();

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
    }

        // Admin: auto-seeding e ripartizione fasce A (30%), B (30%), C (40%) per ELO 1v1 decrescente
    [HttpPost("/api/admin/iscritti/auto-seeding")]
    public async Task<IResult> AutoSeeding()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var lista = (await c.QueryAsync<IdElo>("""
            select i.id, coalesce(g.elo_1v1, g.elo_interno, 0) as elo
            from fanta_torneo.iscritti i
            left join fanta.giocatori g on g.player_id = i.player_id
            where i.stato = 'approvato'
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
    }

    [HttpDelete("/api/admin/iscritti/{id:int}")]
    public async Task<IResult> Elimina(int id)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAdmin(User)) return Results.Forbid();
        await using var c = await db.OpenConnectionAsync();
        await c.ExecuteAsync("delete from fanta_torneo.iscritti where id = @id", new { id });
        return Results.Ok(new { ok = true });
    }

    [HttpPost("/api/admin/iscritti/{id:int}/stato")]
    public async Task<IResult> CambiaStato(int id, RichiestaStatoIscritto req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAdmin(User)) return Results.Forbid();
        var stato = (req.Stato ?? "").Trim().ToLowerInvariant();
        if (stato is not ("in_attesa" or "approvato" or "respinto"))
            return Results.BadRequest(new { errore = "Stato non valido." });
        await using var c = await db.OpenConnectionAsync();
        await c.ExecuteAsync("update fanta_torneo.iscritti set stato = @stato where id = @id", new { id, stato });
        return Results.Ok(new { ok = true });
    }

        // Svincola un giocatore assegnato per errore e rimborsa i Berlu alla squadra
    [HttpPost("/api/admin/iscritti/{id:int}/svincola")]
    public async Task<IResult> Svincola(int id)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        var isc = await c.QuerySingleOrDefaultAsync<Iscritto>(
            "select id, squadra_id, prezzo_asta from fanta_torneo.iscritti where id = @id for update",
            new { id }, tx);
        if (isc is null) return Results.NotFound(new { errore = "Iscritto non trovato." });
        if (isc.SquadraId is null) return Results.BadRequest(new { errore = "Il giocatore non è assegnato a nessuna squadra." });

        if (isc.PrezzoAsta is int prezzo && prezzo > 0)
        {
            await c.ExecuteAsync(
                "update fanta_torneo.squadre set berlu_residui = berlu_residui + @prezzo where id = @sid",
                new { prezzo, sid = isc.SquadraId.Value }, tx);
        }
        await c.ExecuteAsync(
            "update fanta_torneo.iscritti set squadra_id = null, prezzo_asta = null where id = @id",
            new { id }, tx);
        await tx.CommitAsync();
        return Results.Ok(new { ok = true });
    }

        // Export CSV delle rose e degli iscritti
    [HttpGet("/api/torneo/export/rose.csv")]
    public async Task<IResult> EsportaRose()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync<Iscritto>("""
            select i.id, i.nick, trim(i.fascia) as fascia, i.seeding, i.prezzo_base,
                   s.nome as squadra_nome, i.prezzo_asta, i.stato,
                   g.elo_interno, g.elo_1v1, g.elo_tg
            from fanta_torneo.iscritti i
            left join fanta_torneo.squadre s on s.id = i.squadra_id
            left join fanta.giocatori g on g.player_id = i.player_id
            where i.stato = 'approvato'
            order by s.nome asc nulls last, i.fascia asc, i.seeding asc nulls last, i.nick asc
            """);

        static string Csv(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder();
        sb.AppendLine("Squadra,Fascia,Seed,Nick,ELO_1v1,ELO_Interno,ELO_TG,Prezzo_Berlu");
        foreach (var r in righe)
        {
            sb.AppendLine(string.Join(",",
                Csv(r.SquadraNome ?? "Disponibile"),
                Csv(r.Fascia),
                r.Seeding?.ToString() ?? "",
                Csv(r.Nick),
                r.Elo1v1?.ToString() ?? "",
                r.EloInterno?.ToString() ?? "",
                r.EloTg?.ToString() ?? "",
                r.PrezzoAsta?.ToString() ?? ""));
        }
        return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", "fantaage-rose.csv");
    }
}
