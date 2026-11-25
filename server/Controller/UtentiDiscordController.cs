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
public class UtentiDiscordController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public UtentiDiscordController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

        // Admin: utenti Discord visti al login e verifica collegamento
    [HttpGet("/api/admin/utenti-discord")]
    public async Task<IResult> Elenco()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAdmin(User)) return Results.Forbid();
        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync<UtenteDiscord>("""
            select u.discord_id, u.discord_nome, u.discord_avatar, u.player_id,
                   g.nick as player_nick, u.collegato_da, u.verificato, u.aggiornato_il
            from fanta_torneo.utenti_discord u
            left join fanta.giocatori g on g.player_id = u.player_id
            order by u.aggiornato_il desc
            """);
        return Results.Ok(righe);
    }

    [HttpPost("/api/admin/utenti-discord/{discordId}/collega")]
    public async Task<IResult> Collega(string discordId, RichiestaCollega req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAdmin(User)) return Results.Forbid();
        await using var c = await db.OpenConnectionAsync();
        await c.ExecuteAsync("""
            update fanta_torneo.utenti_discord
            set player_id = @pid,
                collegato_da = 'admin',
                verificato = @ver,
                aggiornato_il = now()
            where discord_id = @discordId
            """, new { pid = req.PlayerId, ver = req.Verificato ?? true, discordId });

        return Results.Ok(new { ok = true });
    }
}
