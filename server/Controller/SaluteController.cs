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
public class SaluteController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public SaluteController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    [HttpGet("/api/info")]
    [OutputCache(PolicyName = "breve")]
    public IResult Info() => Results.Ok(new
    {
        handbook = Environment.GetEnvironmentVariable("HANDBOOK_URL"),
    });

    [HttpGet("/api/salute")]
    public async Task<IResult> Stato()
    {
        var dbOk = false;
        if (db is not null)
        {
            try
            {
                await using var c = await db.OpenConnectionAsync();
                dbOk = await c.ExecuteScalarAsync<int>("select 1") == 1;
            }
            catch { dbOk = false; }
        }
        return Results.Ok(new
        {
            stato = dbOk ? "ok" : "db non raggiungibile",
            versione = Environment.GetEnvironmentVariable("VERSIONE") ?? "locale",
            ora = DateTime.UtcNow,
        });
    }
}
