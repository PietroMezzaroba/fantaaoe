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
public class DisponibilitaController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public DisponibilitaController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    [HttpGet("/api/disponibilita/mie")]
    public async Task<IResult> Mie()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        var did = DiscordId(User)!;
        await using var c = await db.OpenConnectionAsync();
        var righe = await c.QueryAsync<DisponibilitaRiga>("""
            select id, discord_id, data, stato, fascia_oraria, note, aggiornata_il
            from fanta_torneo.disponibilita
            where discord_id = @did and data >= current_date - 1 and data <= current_date + 28
            order by data asc
            """, new { did });
        return Results.Ok(righe);
    }

    [HttpPut("/api/disponibilita")]
    public async Task<IResult> Salva(RichiestaDisponibilita req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        var did = DiscordId(User)!;

        if (!DateOnly.TryParse(req.Data, out var dataGiorno))
            return Results.BadRequest(new { errore = "Data non valida (usa YYYY-MM-DD)." });
        var stato = (req.Stato ?? "").Trim().ToLowerInvariant();
        if (stato is not ("disponibile" or "forse" or "indisponibile"))
            return Results.BadRequest(new { errore = "Stato non valido." });

        await using var c = await db.OpenConnectionAsync();
        await c.ExecuteAsync("""
            insert into fanta_torneo.disponibilita (discord_id, data, stato, fascia_oraria, note, aggiornata_il)
            values (@did, @data, @stato, @fascia, @note, now())
            on conflict (discord_id, data) do update
            set stato = excluded.stato,
                fascia_oraria = excluded.fascia_oraria,
                note = excluded.note,
                aggiornata_il = now()
            """, new
        {
            did,
            data = dataGiorno.ToDateTime(TimeOnly.MinValue),
            stato,
            fascia = string.IsNullOrWhiteSpace(req.FasciaOraria) ? null : req.FasciaOraria.Trim(),
            note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
        });
        return Results.Ok(new { ok = true });
    }
}
