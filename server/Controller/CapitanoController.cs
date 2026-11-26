using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using static FantaAoe.Api.TorneoUtil;

namespace FantaAoe.Api.Controllers;

// La squadra del capitano per «La tua pagina» (/io): squadra, Berlu, posto nell'ordine di chiamata, rosa e disponibilità
// dei prossimi 14 giorni di tutti i suoi giocatori. Riscritto il 26/11/2026 sulle tabelle nuove dell'asta (squadre,
// asta_portafoglio_utente, asta_ordine_capitani, asta_giocatori_acquistati, utenti_iscritti): la versione di prima
// leggeva la vecchia tabella "iscritti", che non esiste più.

public sealed class SquadraCapitano
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? IdCapitano { get; set; }
    public int? CreditoIniziale { get; set; }
    public int? CreditoResiduo { get; set; }
    public int Ordine { get; set; }
}

public sealed class MembroRosa
{
    public string IdDiscord { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Fascia { get; set; }
    public int? Elo { get; set; }
    public int? Costo { get; set; }
    public bool Capitano { get; set; }
}

[ApiController]
public class CapitanoController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public CapitanoController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    // La propria squadra (un admin può chiederne una qualsiasi con ?squadraId=)
    [HttpGet("/api/capitano/squadra")]
    public async Task<IResult> Squadra(int? squadraId)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!ECapitano(User)) return Results.Forbid();
        var did = DiscordId(User)!;

        await using var c = await db.OpenConnectionAsync();
        const string Campi = """
            select s.id, s.nome, s.id_capitano, p.credito_iniziale, p.credito_residuo, coalesce(o.ordine, 0) as ordine
            from fanta_torneo.squadre s
            left join fanta_torneo.asta_portafoglio_utente p on p.id_utente = s.id_capitano
            left join fanta_torneo.asta_ordine_capitani o on o.id_utente = s.id_capitano
            """;
        var squadra = squadraId is int sid && EAdmin(User)
            ? await c.QuerySingleOrDefaultAsync<SquadraCapitano>(Campi + " where s.id = @sid", new { sid })
            : await c.QuerySingleOrDefaultAsync<SquadraCapitano>(Campi + " where s.id_capitano = @did order by s.id limit 1", new { did });

        if (squadra is null)
            return Results.Ok(new { squadra = (SquadraCapitano?)null, rosa = Array.Empty<MembroRosa>(), disponibilita = Array.Empty<DisponibilitaRiga>() });

        // la rosa: chi la squadra ha comprato all'asta (il capitano compreso, a costo 0)
        var rosa = (await c.QueryAsync<MembroRosa>("""
            select a.id_iscritto as id_discord, coalesce(i.nickname, u.discord_nome, '') as nome, i.fascia,
                   i.elo_calcolato as elo, a.costo_acquisto as costo, coalesce(a.id_iscritto = s.id_capitano, false) as capitano
            from fanta_torneo.asta_giocatori_acquistati a
            join fanta_torneo.squadre s on s.id = a.id_squadra
            left join fanta_torneo.utenti_iscritti i on i.id_discord = a.id_iscritto
            left join fanta_torneo.utenti_discord u on u.discord_id = a.id_iscritto
            where a.id_squadra = @sid
            order by coalesce(a.id_iscritto = s.id_capitano, false) desc, i.elo_calcolato desc nulls last, a.id
            """, new { sid = squadra.Id })).ToList();

        // il capitano c'è sempre, anche prima di essere inserito nella rosa
        if (squadra.IdCapitano is string cap && rosa.All(r => r.IdDiscord != cap))
        {
            var nomeCap = await c.ExecuteScalarAsync<string?>(
                "select discord_nome from fanta_torneo.utenti_discord where discord_id = @cap", new { cap });
            rosa.Insert(0, new MembroRosa { IdDiscord = cap, Nome = nomeCap ?? "", Capitano = true });
        }

        var ids = rosa.Select(r => r.IdDiscord).Distinct().ToArray();
        var disponibilita = await c.QueryAsync<DisponibilitaRiga>("""
            select id, discord_id, data, stato, fascia_oraria, note, aggiornata_il
            from fanta_torneo.disponibilita
            where discord_id = any(@ids) and data >= current_date and data < current_date + 14
            order by data asc
            """, new { ids });

        return Results.Ok(new { squadra, rosa, disponibilita });
    }
}
