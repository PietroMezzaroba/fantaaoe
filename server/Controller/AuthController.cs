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
public class AuthController : ControllerBase
{
    private readonly NpgsqlDataSource? db;
    private readonly ConfigAccesso accesso;
    private readonly IWebHostEnvironment ambiente;

    public AuthController(IWebHostEnvironment ambiente, ConfigAccesso accesso, NpgsqlDataSource? db = null)
    {
        this.ambiente = ambiente;
        this.db = db;
        this.accesso = accesso;
    }

    [HttpGet("/api/auth/login")]
    public IResult Login(string? ritorno) =>
        accesso.Attivo
            ? Results.Challenge(new AuthenticationProperties { RedirectUri = Accesso.RitornoSicuro(ritorno) }, ["Discord"])
            : Results.Redirect("/?accesso=non-configurato");

    [HttpGet("/api/auth/esci")]
    public async Task<IResult> Esci()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/");
    }

    [HttpGet("/api/io")]
    public async Task<IResult> Io()
    {
        var dentro = User.Identity?.IsAuthenticated == true;
        var admin = User.IsInRole(Accesso.Admin);
        if (dentro) await RegistraUtenteDiscord(db, User);
        var impostazioni = dentro ? await LeggiImpostazioni(db, User) : null;
        return Results.Ok(new
        {
            loginAttivo = accesso.Attivo,
            loginSviluppo = ambiente.IsDevelopment(),
            autenticato = dentro,
            discordId = dentro ? User.FindFirst("discord_id")?.Value : null,
            nome = dentro ? User.FindFirst("nome")?.Value : null,
            avatar = dentro ? User.FindFirst("avatar")?.Value : null,
            impostazioni,
            membro = dentro && User.HasClaim(c => c.Type == "membro"),
            ruoli = dentro ? User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() : Array.Empty<string>(),
            // solo per gli admin: serve a collegare i ruoli Discord ai pannelli
            configurazione = admin ? new
            {
                discordId = User.FindFirst("discord_id")?.Value,
                guildId = User.FindFirst("guild_id")?.Value,
                ruoliDiscord = User.FindAll("ruolo_discord").Select(c => c.Value).ToArray(),
            } : null,
        });
    }
}
