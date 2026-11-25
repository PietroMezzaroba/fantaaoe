using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace FantaAoe.Api.Controllers;

/// <summary>
/// Login simulato per lo sviluppo: sostituisce Discord leggendo fanta_torneo.utenti_dev e i ruoli (anche più di uno) da fanta_torneo.utenti_ruoli.
/// Esiste solo in Development (vedi [SoloSviluppo] e Program.cs): in produzione la rotta non c'è.
/// </summary>
[ApiController]
[SoloSviluppo]
public class AccessoSviluppoController : ControllerBase
{
    private readonly NpgsqlDataSource? db;
    private readonly IWebHostEnvironment ambiente;

    public AccessoSviluppoController(IWebHostEnvironment ambiente, NpgsqlDataSource? db = null)
    {
        this.ambiente = ambiente;
        this.db = db;
    }

    public sealed record RichiestaAccessoSviluppo(string? Username, string? Password);

    private sealed class UtenteSviluppo { public string IdDiscord { get; set; } = ""; public string Username { get; set; } = ""; }

    [HttpPost("/api/auth/dev-login")]
    public async Task<IResult> Accedi(RichiestaAccessoSviluppo req)
    {
        if (!ambiente.IsDevelopment()) return Results.NotFound();
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);

        await using var c = await db.OpenConnectionAsync();
        var utente = await c.QuerySingleOrDefaultAsync<UtenteSviluppo>("""
            select u.id_discord, u.username
            from fanta_torneo.utenti_dev u
            where u.username = @u and u.password = @p
            """, new { u = req.Username ?? "", p = req.Password ?? "" });
        if (utente is null) return Results.Json(new { errore = "Username o password errati" }, statusCode: 401);

        // gli stessi claim che in produzione mette Accesso.CompletaTicket; i ruoli vengono da utenti_ruoli
        var claims = new List<Claim>
        {
            new("discord_id", utente.IdDiscord),
            new("nome", utente.Username),
            new("membro", "1"),
        };
        // un claim per ogni ruolo dell'utente (fanta_torneo.utenti_ruoli); nessuna riga = nessun ruolo
        var ruoli = await c.QueryAsync<string>("""
            select r.chiave_ruolo
            from fanta_torneo.utenti_ruoli ur
            join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
            where ur.id_utente = @did
            order by r.id_ruolo
            """, new { did = utente.IdDiscord });
        foreach (var ruolo in ruoli) claims.Add(new(ClaimTypes.Role, ruolo));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });
        return Results.Ok(new { ok = true });
    }
}
