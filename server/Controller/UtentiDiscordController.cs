using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using static FantaAoe.Api.TorneoUtil;

namespace FantaAoe.Api.Controllers;

public sealed class UtenteDiscordAdmin
{
    public string DiscordId { get; set; } = "";
    public string DiscordNome { get; set; } = "";
    public string? DiscordAvatar { get; set; }
    public int? PlayerId { get; set; }
    public string? PlayerNick { get; set; }
    public string? PlayerClan { get; set; }
    public int? EloInterno { get; set; }
    public int? Elo1v1 { get; set; }
    public int? EloTg { get; set; }
    public int? NDiti { get; set; }
    public string CollegatoDa { get; set; } = "auto";
    public bool Verificato { get; set; }
    public DateTime AggiornatoIl { get; set; }
    public bool Iscritto { get; set; }
    public string? IscrittoNickname { get; set; }
    public string[] Ruoli { get; set; } = [];
}

public sealed record RichiestaCollegaAdmin(int? PlayerId, bool? Verificato, bool? Forza = null);

[ApiController]
public class UtentiDiscordController : ControllerBase
{
    private readonly NpgsqlDataSource? db;
    private readonly PresenzeSito sito;
    private readonly PresenzeAsta presenzeAsta;
    private readonly IHubContext<AstaHub> hub;

    public UtentiDiscordController(PresenzeSito sito, PresenzeAsta presenzeAsta, IHubContext<AstaHub> hub, NpgsqlDataSource? db = null)
    {
        this.sito = sito;
        this.presenzeAsta = presenzeAsta;
        this.hub = hub;
        this.db = db;
    }

    // Admin: associazioni Discord <-> nick AoE2, con priorità ai match simili di Thomuggia e a chi non ha ancora un nick
    [HttpGet("/api/admin/utenti-discord")]
    public async Task<IResult> Elenco()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync<UtenteDiscordAdmin>("""
            select u.discord_id, u.discord_nome, u.discord_avatar, u.player_id,
                   g.nick as player_nick, g.clan as player_clan,
                   g.elo_interno, g.elo_1v1, g.elo_tg, g.n_diti,
                   u.collegato_da, u.verificato, u.aggiornato_il,
                   (i.id_discord is not null) as iscritto,
                   i.nickname as iscritto_nickname,
                   coalesce(rr.chiavi, array[]::text[]) as ruoli
            from fanta_torneo.utenti_discord u
            left join fanta.giocatori g on g.player_id = u.player_id
            left join fanta_torneo.utenti_iscritti i on i.id_discord = u.discord_id
            left join lateral (
                select array_agg(r.chiave_ruolo order by r.id_ruolo) as chiavi
                from fanta_torneo.utenti_ruoli ur
                join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                where ur.id_utente = u.discord_id
            ) rr on true
            order by
                case
                    when u.collegato_da = 'thomuggia_simile' and not u.verificato then 0
                    when u.collegato_da = 'utente' and not u.verificato then 1
                    when u.player_id is null then 2
                    when not u.verificato then 3
                    else 4
                end,
                lower(u.discord_nome),
                u.discord_id
            """);
        return Results.Ok(righe);
    }

    private sealed class DuplicatoDiscord
    {
        public string DiscordId { get; set; } = "";
        public string DiscordNome { get; set; } = "";
    }

    [HttpPost("/api/admin/utenti-discord/{discordId}/collega")]
    public async Task<IResult> Collega(string discordId, RichiestaCollegaAdmin req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();

        await using var c = await db.OpenConnectionAsync();
        var esisteUtente = await c.ExecuteScalarAsync<bool>(
            "select exists(select 1 from fanta_torneo.utenti_discord where discord_id = @discordId)", new { discordId });
        if (!esisteUtente) return Results.NotFound(new { errore = "Utente Discord non trovato." });

        string? playerNick = null;
        bool verificato;

        if (req.PlayerId is int pid)
        {
            playerNick = await c.ExecuteScalarAsync<string?>(
                "select nick from fanta.giocatori where player_id = @pid", new { pid });
            if (playerNick is null)
                return Results.BadRequest(new { errore = "Giocatore non trovato in classifica." });

            if (req.Forza != true)
            {
                var dup = await c.QueryFirstOrDefaultAsync<DuplicatoDiscord>("""
                    select discord_id, discord_nome
                    from fanta_torneo.utenti_discord
                    where player_id = @pid and discord_id <> @discordId
                    limit 1
                    """, new { pid, discordId });
                if (dup is not null)
                {
                    return Results.Conflict(new
                    {
                        errore = $"Il nick {playerNick} è già collegato a {dup.DiscordNome}.",
                        duplicato = new { discordId = dup.DiscordId, discordNome = dup.DiscordNome },
                    });
                }
            }

            verificato = req.Verificato ?? true;
            await c.ExecuteAsync("""
                update fanta_torneo.utenti_discord
                set player_id = @pid,
                    collegato_da = 'admin',
                    verificato = @ver,
                    aggiornato_il = now()
                where discord_id = @discordId
                """, new { pid, ver = verificato, discordId });
        }
        else
        {
            verificato = false;
            await c.ExecuteAsync("""
                update fanta_torneo.utenti_discord
                set player_id = null,
                    collegato_da = 'admin',
                    verificato = false,
                    aggiornato_il = now()
                where discord_id = @discordId
                """, new { discordId });
        }

        if (sito.AggiornaProfilo(discordId, req.PlayerId, playerNick))
            await hub.Clients.Group(AstaHub.GruppoAdminOnline).SendAsync(AstaHub.EventoOnline, sito.Istantanea(presenzeAsta));

        return Results.Ok(new
        {
            ok = true,
            playerId = req.PlayerId,
            playerNick,
            collegatoDa = "admin",
            verificato,
        });
    }
}
