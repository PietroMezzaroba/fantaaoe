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
public class GiocatoriController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public GiocatoriController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    [OutputCache(PolicyName = "breve")]
    [HttpGet("/api/giocatori")]
    public async Task<IResult> Elenco(bool? tutti)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync<Giocatore>("""
            select player_id, nick, clan, elo_interno, elo_1v1, elo_tg,
                   n_diti, n_diti_vinti, winrate, ultimo_dito, n_account
            from giocatori
            where @tutti or ultimo_dito > now() - interval '90 days'
            order by elo_interno desc, nick
            """, new { tutti = tutti ?? false });
        return Results.Ok(righe);
    }

    [OutputCache(PolicyName = "breve")]
    [HttpGet("/api/giocatori/{id:int}")]
    public async Task<IResult> Dettaglio(int id)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        await using var c = await db.OpenConnectionAsync();

        var g = await c.QuerySingleOrDefaultAsync<GiocatoreDettaglio>(
            "select * from giocatori where player_id = @id", new { id });
        if (g is null) return Results.NotFound();

        var account = await c.QueryAsync<Account>("""
            select aoe2_id, steam_name, country, mmr_1v1, max_1v1_mmr, mmr_tg, max_tg_mmr,
                   games_1v1, games_tg, last_game_1v1, last_game_tg, status
            from account where player_id = @id
            order by greatest(coalesce(mmr_1v1, 0), coalesce(mmr_tg, 0)) desc
            """, new { id });

        var partite = await c.QueryAsync<PartitaDelGiocatore>("""
            select p.id_game, p.started_at, p.durata_in_game_minuti, p.dito,
                   pg.vinto, pg.civ, pg.elo_delta, pg.elo_after
            from partite_giocatori pg join partite p using (id_game)
            where pg.player_id = @id
            order by p.started_at desc nulls last, p.id_game desc
            limit 30
            """, new { id });

        var andamento = await c.QueryAsync<PuntoElo>("""
            select p.started_at as data, pg.elo_after as elo
            from partite_giocatori pg join partite p using (id_game)
            where pg.player_id = @id and p.valida and pg.elo_after is not null
            order by p.started_at nulls first, p.id_game
            """, new { id });

        var civ = await c.QueryAsync<CivStat>("""
            select pg.civ, count(*) as partite, sum(pg.vinto) as vittorie
            from partite_giocatori pg join partite p using (id_game)
            where pg.player_id = @id and p.valida and pg.civ is not null
            group by pg.civ
            order by count(*) desc, sum(pg.vinto) desc
            limit 12
            """, new { id });

        return Results.Ok(new { giocatore = g, account, partite, andamento, civ });
    }
}
