using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using static FantaAoe.Api.TorneoUtil;

namespace FantaAoe.Api.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly NpgsqlDataSource? db;
    private readonly ConfigAccesso accesso;
    private readonly IWebHostEnvironment ambiente;

    private readonly PresenzeAsta presenze;
    private readonly PresenzeSito sito;
    private readonly IHubContext<AstaHub> hub;

    public AuthController(IWebHostEnvironment ambiente, ConfigAccesso accesso, PresenzeAsta presenze, PresenzeSito sito, IHubContext<AstaHub> hub, NpgsqlDataSource? db = null)
    {
        this.presenze = presenze;
        this.sito = sito;
        this.hub = hub;
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
        // chi esce non è più sulla pagina dell'asta né tra gli utenti online: lo si toglie subito senza aspettare il timeout
        var utente = EAutenticato(User) ? DiscordId(User) : null;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (utente is not null)
        {
            var toltoAsta = presenze.RimuoviUtente(utente);
            var toltoSito = sito.RimuoviUtente(utente);
            if (toltoAsta) await hub.Clients.All.SendAsync(AstaHub.EventoPresenze, presenze.Elenco());
            if (toltoSito || toltoAsta) await hub.Clients.Group(AstaHub.GruppoAdminOnline).SendAsync(AstaHub.EventoOnline, sito.Istantanea(presenze));
        }
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
