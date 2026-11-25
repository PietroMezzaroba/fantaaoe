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
public class CivController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public CivController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    // Anagrafica delle civiltà (tabella fanta_torneo.civilta): nome, nome del file dello stemma e descrizione
    [OutputCache(PolicyName = "breve")]
    [HttpGet("/api/civilta")]
    public async Task<IResult> Anagrafica()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync(
            "select id, nome, avatar, descrizione from fanta_torneo.civilta order by nome");
        return Results.Ok(righe);
    }

    [OutputCache(PolicyName = "breve")]
    [HttpGet("/api/civ")]
    public async Task<IResult> Elenco(int? giorni)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync<CivStat>("""
            select pg.civ, count(*) as partite, sum(pg.vinto) as vittorie
            from partite_giocatori pg join partite p using (id_game)
            where p.valida and pg.civ is not null
              and (cast(@giorni as int) is null or p.started_at > now() - make_interval(days => cast(@giorni as int)))
            group by pg.civ
            order by count(*) desc
            """, new { giorni });
        return Results.Ok(righe);
    }
}
