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
public class CapitanoController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public CapitanoController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

        // Vista Capitano: la propria squadra (o una squadra scelta dall'admin), rosa e disponibilità dei prossimi 14 giorni
    [HttpGet("/api/capitano/squadra")]
    public async Task<IResult> Squadra(int? squadraId)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!ECapitano(User)) return Results.Forbid();
        var did = DiscordId(User)!;

        await using var c = await db.OpenConnectionAsync();
        Squadra? squadra = null;
        if (squadraId is int sid && EAdmin(User))
        {
            squadra = await c.QuerySingleOrDefaultAsync<Squadra>("""
                select s.id, s.nome, s.capitano_discord_id, s.capitano_nome,
                       s.berlu_iniziali, s.berlu_residui, s.ordine_chiamata, s.canale_discord_url,
                       (select count(*) from fanta_torneo.iscritti i where i.squadra_id = s.id)::int as n_giocatori
                from fanta_torneo.squadre s where s.id = @sid
                """, new { sid });
        }
        else
        {
            squadra = await c.QuerySingleOrDefaultAsync<Squadra>("""
                select s.id, s.nome, s.capitano_discord_id, s.capitano_nome,
                       s.berlu_iniziali, s.berlu_residui, s.ordine_chiamata, s.canale_discord_url,
                       (select count(*) from fanta_torneo.iscritti i where i.squadra_id = s.id)::int as n_giocatori
                from fanta_torneo.squadre s where s.capitano_discord_id = @did limit 1
                """, new { did });
        }

        if (squadra is null) return Results.Ok(new { squadra = (Squadra?)null, rosa = Array.Empty<Iscritto>(), disponibilita = Array.Empty<DisponibilitaRiga>() });

        var rosa = (await c.QueryAsync<Iscritto>("""
            select i.id, i.player_id, i.discord_id, i.nick, trim(i.fascia) as fascia,
                   i.seeding, i.prezzo_base, i.squadra_id, s.nome as squadra_nome,
                   i.prezzo_asta, i.note,
                   g.elo_interno, g.elo_1v1, g.elo_tg, g.n_diti, g.winrate
            from fanta_torneo.iscritti i
            left join fanta_torneo.squadre s on s.id = i.squadra_id
            left join fanta.giocatori g on g.player_id = i.player_id
            where i.squadra_id = @sid
            order by i.fascia asc, i.seeding asc nulls last, i.nick asc
            """, new { sid = squadra.Id })).ToList();

        var dids = rosa.Where(r => !string.IsNullOrEmpty(r.DiscordId)).Select(r => r.DiscordId!).Distinct().ToArray();
        IEnumerable<DisponibilitaRiga> disp = [];
        if (dids.Length > 0)
        {
            disp = await c.QueryAsync<DisponibilitaRiga>("""
                select id, discord_id, data, stato, fascia_oraria, note, aggiornata_il
                from fanta_torneo.disponibilita
                where discord_id = any(@dids) and data >= current_date and data <= current_date + 14
                order by data asc
                """, new { dids });
        }

        return Results.Ok(new { squadra, rosa, disponibilita = disp });
    }
}
