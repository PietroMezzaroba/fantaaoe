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
public class StatisticheController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public StatisticheController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    [OutputCache(PolicyName = "breve")]
    [HttpGet("/api/statistiche")]
    public async Task<IResult> Numeri()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        await using var c = await db.OpenConnectionAsync();
        var numeri = await c.QuerySingleAsync<Statistiche>("""
            select
              (select count(*) from giocatori) as giocatori_totali,
              (select count(*) from giocatori where ultimo_dito > now() - interval '90 days') as attivi90,
              (select count(*) from giocatori where ultimo_dito > now() - interval '30 days') as attivi30,
              (select count(*) from partite where valida) as partite_valide,
              (select count(*) from partite where valida and started_at > now() - interval '30 days') as partite30,
              (select count(*) from partite where valida and started_at > now() - interval '7 days') as partite7,
              (select coalesce(sum(durata_in_game_minuti), 0) from partite where valida) as minuti_totali,
              (select max(dito) from partite) as ultimo_dito,
              (select min(started_at) from partite) as prima_partita
            """);
        var settimane = await c.QueryAsync<Settimana>("""
            select date_trunc('week', s)::timestamp as inizio,
                   (select count(*) from partite p
                     where p.valida and p.started_at >= date_trunc('week', s) and p.started_at < date_trunc('week', s) + interval '7 days') as partite
            from generate_series(now() - interval '15 weeks', now(), interval '1 week') s
            order by 1
            """);
        return Results.Ok(new { numeri, settimane });
    }
}
