// Login con Discord. Dopo il consenso, con il token dell'utente (non quello del bot) si leggono:
//  - i suoi server (scope "guilds"): se è amministratore del server AoE Italia diventa admin;
//  - i suoi ruoli nel server (scope "guilds.members.read"): capitano, iscritto, streamer secondo la configurazione.
// Configurazione da variabili d'ambiente (mai nel repo; in sviluppo anche da appsettings.Development.json):
//   DISCORD_CLIENT_ID, DISCORD_CLIENT_SECRET  app del portale sviluppatori Discord
//   PUBLIC_URL                                indirizzo pubblico, es. https://fantaaoe.tail17199a.ts.net
//   DISCORD_GUILD_ID                          ID del server; se manca si cerca per nome ("Age of Empires - Italia")
//   RUOLI_ISCRITTO, RUOLI_CAPITANO, RUOLI_STREAMER, RUOLI_ADMIN   ID dei ruoli Discord, separati da virgola
//   ADMIN_UTENTI                              ID utente Discord sempre admin, separati da virgola

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace FantaAoe.Api;

public sealed class ConfigAccesso
{
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? IndirizzoPubblico { get; init; }
    public string? GuildId { get; init; }
    public string NomeGuild { get; init; } = "Age of Empires - Italia";
    public HashSet<string> RuoliIscritto { get; init; } = [];
    public HashSet<string> RuoliCapitano { get; init; } = [];
    public HashSet<string> RuoliStreamer { get; init; } = [];
    public HashSet<string> RuoliAdmin { get; init; } = [];
    public HashSet<string> AdminUtenti { get; init; } = [];

    public bool Attivo => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    static HashSet<string> Lista(string? v) =>
        (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();

    // Legge da IConfiguration: le variabili d'ambiente (produzione) e, in sviluppo, appsettings.Development.json.
    public static ConfigAccesso DaAmbiente(IConfiguration c) => new()
    {
        ClientId = c["DISCORD_CLIENT_ID"],
        ClientSecret = c["DISCORD_CLIENT_SECRET"],
        IndirizzoPubblico = c["PUBLIC_URL"],
        GuildId = c["DISCORD_GUILD_ID"],
        NomeGuild = c["DISCORD_GUILD_NOME"] ?? "Age of Empires - Italia",
        // ruolo "Dito" del server (lo stesso che il bot tagga per i TG Dito)
        RuoliIscritto = Lista(c["RUOLI_ISCRITTO"] ?? "1436121682614947980"),
        RuoliCapitano = Lista(c["RUOLI_CAPITANO"]),
        RuoliStreamer = Lista(c["RUOLI_STREAMER"]),
        RuoliAdmin = Lista(c["RUOLI_ADMIN"]),
        AdminUtenti = Lista(c["ADMIN_UTENTI"]),
    };
}

public static class Accesso
{
    public const string Admin = "admin";
    public const string Capitano = "capitano";
    public const string Iscritto = "giocatore";
    public const string Streamer = "streamer";

    const long PermessoAmministratore = 0x8;

    // Riempie i claim del ticket con nome, avatar e ruoli dell'app.
    public static async Task CompletaTicket(OAuthCreatingTicketContext ctx, ConfigAccesso cfg)
    {
        var id = ctx.Identity!;
        var http = ctx.Backchannel;

        async Task<JsonDocument?> Leggi(string percorso)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://discord.com/api/v10{percorso}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
            using var r = await http.SendAsync(req, ctx.HttpContext.RequestAborted);
            if (!r.IsSuccessStatusCode) return null;
            return await JsonDocument.ParseAsync(await r.Content.ReadAsStreamAsync(ctx.HttpContext.RequestAborted));
        }

        using var utente = await Leggi("/users/@me");
        if (utente is not null)
        {
            var u = utente.RootElement;
            var uid = u.GetProperty("id").GetString()!;
            var nome = u.TryGetProperty("global_name", out var gn) && gn.ValueKind == JsonValueKind.String
                ? gn.GetString()! : u.GetProperty("username").GetString()!;
            id.AddClaim(new Claim("discord_id", uid));
            id.AddClaim(new Claim("nome", nome));
            if (u.TryGetProperty("avatar", out var av) && av.ValueKind == JsonValueKind.String)
                id.AddClaim(new Claim("avatar", $"https://cdn.discordapp.com/avatars/{uid}/{av.GetString()}.png?size=64"));
        }
        var discordId = id.FindFirst("discord_id")?.Value ?? "";

        // il server della community: per ID se configurato, altrimenti per nome
        string? guildId = cfg.GuildId;
        var amministratore = false;
        using (var server = await Leggi("/users/@me/guilds"))
        {
            if (server is not null)
            {
                foreach (var g in server.RootElement.EnumerateArray())
                {
                    var gid = g.GetProperty("id").GetString();
                    var nomeG = g.GetProperty("name").GetString();
                    var giusto = guildId is not null ? gid == guildId : string.Equals(nomeG, cfg.NomeGuild, StringComparison.OrdinalIgnoreCase);
                    if (!giusto) continue;
                    guildId = gid;
                    var proprietario = g.TryGetProperty("owner", out var ow) && ow.ValueKind == JsonValueKind.True;
                    var permessi = g.TryGetProperty("permissions", out var pm) && long.TryParse(pm.GetString(), out var p) ? p : 0;
                    amministratore = proprietario || (permessi & PermessoAmministratore) != 0;
                    id.AddClaim(new Claim("membro", "1"));
                    break;
                }
            }
        }
        if (guildId is not null) id.AddClaim(new Claim("guild_id", guildId));

        var ruoliDiscord = new List<string>();
        if (guildId is not null && id.HasClaim(c => c.Type == "membro"))
        {
            using var membro = await Leggi($"/users/@me/guilds/{guildId}/member");
            if (membro is not null && membro.RootElement.TryGetProperty("roles", out var roles))
                ruoliDiscord.AddRange(roles.EnumerateArray().Select(r => r.GetString()!).Where(r => r is not null));
            if (membro is not null && membro.RootElement.TryGetProperty("nick", out var nick) && nick.ValueKind == JsonValueKind.String)
            {
                var vecchio = id.FindFirst("nome");
                if (vecchio is not null) id.RemoveClaim(vecchio);
                id.AddClaim(new Claim("nome", nick.GetString()!));
            }
        }
        foreach (var r in ruoliDiscord) id.AddClaim(new Claim("ruolo_discord", r));

        var ruoli = new HashSet<string>();
        if (amministratore || cfg.AdminUtenti.Contains(discordId) || ruoliDiscord.Any(cfg.RuoliAdmin.Contains)) ruoli.Add(Admin);
        if (ruoliDiscord.Any(cfg.RuoliCapitano.Contains)) ruoli.Add(Capitano);
        if (ruoliDiscord.Any(cfg.RuoliStreamer.Contains)) ruoli.Add(Streamer);
        if (ruoliDiscord.Any(cfg.RuoliIscritto.Contains)) ruoli.Add(Iscritto);

        // i ruoli veri stanno nel database (utenti_ruoli): un claim per ogni ruolo dell'utente.
        // Se il database non risponde si usano quelli ricavati da Discord, così il login non si blocca.
        IEnumerable<string> ruoliFinali = ruoli;
        var db = ctx.HttpContext.RequestServices.GetService<Npgsql.NpgsqlDataSource>();
        if (db is not null && discordId != "")
        {
            try
            {
                ruoliFinali = await TorneoUtil.RegistraEAssegna(db, discordId, id.FindFirst("nome")?.Value ?? "Utente",
                    id.FindFirst("avatar")?.Value, ruoli.ToList());
            }
            catch { /* resta il fallback */ }
        }
        foreach (var r in ruoliFinali) id.AddClaim(new Claim(ClaimTypes.Role, r));
    }

    // Solo percorsi interni come ritorno dopo il login (niente redirect verso altri siti).
    public static string RitornoSicuro(string? ritorno) =>
        !string.IsNullOrEmpty(ritorno) && ritorno.StartsWith('/') && !ritorno.StartsWith("//") && !ritorno.StartsWith("/\\")
            ? ritorno : "/";
}
