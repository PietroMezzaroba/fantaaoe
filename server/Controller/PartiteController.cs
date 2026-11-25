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
public class PartiteController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public PartiteController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    [OutputCache(PolicyName = "breve")]
    [HttpGet("/api/partite")]
    public async Task<IResult> Elenco(int? pagina, int? perPagina)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        var n = Math.Clamp(perPagina ?? 30, 1, 100);
        var p = Math.Max(pagina ?? 1, 1);
        await using var c = await db.OpenConnectionAsync();

        var partite = (await c.QueryAsync<Partita>("""
            select id_game, started_at, durata_in_game_minuti, durata_irl_minuti, map_name, valida, dito
            from partite
            order by started_at desc nulls last, id_game desc
            limit @n offset @salta
            """, new { n, salta = (p - 1) * n })).ToList();

        var ids = partite.Select(x => x.IdGame).ToArray();
        var giocatori = (await c.QueryAsync<GiocatoreInPartita>("""
            select id_game, aoe2_id, player_id, nick, vinto, civ, elo_before, elo_delta, elo_after
            from partite_giocatori where id_game = any(@ids)
            order by elo_before desc nulls last
            """, new { ids })).ToLookup(x => x.IdGame);

        var totale = await c.ExecuteScalarAsync<long>("select count(*) from partite");

        return Results.Ok(new
        {
            pagina = p,
            perPagina = n,
            totale,
            partite = partite.Select(x => new { partita = x, giocatori = giocatori[x.IdGame] }),
        });
    }

    [OutputCache(PolicyName = "breve")]
    [HttpGet("/api/partite/{id:long}")]
    public async Task<IResult> Dettaglio(long id)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        await using var c = await db.OpenConnectionAsync();
        var partita = await c.QuerySingleOrDefaultAsync<Partita>("""
            select id_game, started_at, durata_in_game_minuti, durata_irl_minuti, map_name, valida, dito
            from partite where id_game = @id
            """, new { id });
        if (partita is null) return Results.NotFound();
        var giocatori = await c.QueryAsync<GiocatoreInPartita>("""
            select id_game, aoe2_id, player_id, nick, vinto, civ, elo_before, elo_delta, elo_after
            from partite_giocatori where id_game = @id
            order by vinto desc, elo_before desc nulls last
            """, new { id });
        return Results.Ok(new { partita, giocatori });
    }
}
