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
public class SquadreController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public SquadreController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

        // Squadre (pubbliche + gestione admin)
    [HttpGet("/api/torneo/squadre")]
    public async Task<IResult> Elenco()
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
    }
}
