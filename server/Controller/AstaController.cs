using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using static FantaAoe.Api.TorneoUtil;

namespace FantaAoe.Api.Controllers;

/// <summary>Pagina dell'asta (riscritta da capo). Per ora: le squadre con capitano e giocatori.</summary>
[ApiController]
public class AstaController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public AstaController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    private sealed record RigaSquadra(int Id, string Nome, string? Capitano, int? Credito);
    private sealed record RigaGiocatore(int IdSquadra, string IdDiscord, string? Nome, int Elo, int Costo, bool Capitano);

    // Le squadre con il nome del capitano e i loro giocatori (asta_giocatori_acquistati): per ognuno nome, ELO e costo
    // di acquisto. Il capitano è anche un giocatore (a costo 0). L'ordine è per ELO decrescente.
    [HttpGet("/api/asta/squadre")]
    public async Task<IResult> Squadre()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);

        await using var c = await db.OpenConnectionAsync();
        var squadre = (await c.QueryAsync<RigaSquadra>("""
            select s.id, s.nome, u.discord_nome as capitano, p.credito_residuo as credito
            from fanta_torneo.squadre s
            left join fanta_torneo.utenti_discord u on u.discord_id = s.id_capitano
            left join fanta_torneo.asta_portafoglio_utente p on p.id_utente = s.id_capitano
            order by s.id
            """)).ToList();
        var giocatori = (await c.QueryAsync<RigaGiocatore>("""
            select a.id_squadra as idsquadra, a.id_iscritto as iddiscord, i.nickname as nome, i.elo_calcolato as elo, a.costo_acquisto as costo,
                   (a.id_iscritto = s.id_capitano) as capitano
            from fanta_torneo.asta_giocatori_acquistati a
            join fanta_torneo.squadre s on s.id = a.id_squadra
            join fanta_torneo.utenti_iscritti i on i.id_discord = a.id_iscritto
            order by i.elo_calcolato desc, a.id
            """)).ToLookup(g => g.IdSquadra);

        // "tu": il giocatore è l'utente connesso (si manda solo questo, non gli id degli altri)
        var io = EAutenticato(User) ? DiscordId(User) : null;

        return Results.Ok(squadre.Select(s => new
        {
            s.Id,
            s.Nome,
            s.Capitano,
            s.Credito, // crediti rimasti: asta_portafoglio_utente.credito_residuo del capitano (null se non ha un portafoglio)
            Giocatori = giocatori[s.Id].Select(g => new { Nome = g.Nome ?? "", g.Elo, g.Costo, g.Capitano, Tu = io is not null && g.IdDiscord == io }).ToList(),
        }));
    }

    // Giocatori ancora disponibili: gli iscritti validati non ancora in nessuna squadra (asta_giocatori_acquistati), per ELO decrescente.
    [HttpGet("/api/asta/giocatori-disponibili")]
    public async Task<IResult> GiocatoriDisponibili()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync("""
            select i.id_discord as "idDiscord", coalesce(i.nickname, '') as nome, i.elo_calcolato as elo
            from fanta_torneo.utenti_iscritti i
            where i.stato_validazione = 'validato'
              and not exists (select 1 from fanta_torneo.asta_giocatori_acquistati a where a.id_iscritto = i.id_discord)
            order by i.elo_calcolato desc, lower(i.nickname), i.id_discord
            """);
        return Results.Ok(righe);
    }
}
