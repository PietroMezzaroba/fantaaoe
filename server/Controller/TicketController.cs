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
public class TicketController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public TicketController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

        // Ticket: lista (tutti per admin, propri per giocatore)
    [HttpGet("/api/ticket")]
    public async Task<IResult> Elenco(bool? tutti)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        var did = DiscordId(User)!;
        var vediTutti = (tutti ?? false) && EAdmin(User);

        await using var c = await db.OpenConnectionAsync();
        var righe = vediTutti
            ? await c.QueryAsync<TicketRiga>("select * from fanta_torneo.ticket order by case stato when 'aperto' then 1 when 'in_corso' then 2 else 3 end, creato_il desc limit 100")
            : await c.QueryAsync<TicketRiga>("select * from fanta_torneo.ticket where autore_discord_id = @did order by creato_il desc limit 50", new { did });

        return Results.Ok(righe);
    }

    [HttpPost("/api/ticket")]
    public async Task<IResult> Apri(RichiestaTicket req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();

        var tipo = (req.Tipo ?? "segnalazione").Trim().ToLowerInvariant();
        if (tipo is not ("restart" or "segnalazione" or "infrazione" or "altro"))
            return Results.BadRequest(new { errore = "Tipo di ticket non valido." });

        var titolo = req.Titolo?.Trim() ?? "";
        var desc = req.Descrizione?.Trim() ?? "";
        if (titolo.Length < 3 || desc.Length < 5)
            return Results.BadRequest(new { errore = "Inserisci un titolo e una breve descrizione." });

        await using var c = await db.OpenConnectionAsync();
        var id = await c.ExecuteScalarAsync<int>("""
            insert into fanta_torneo.ticket (autore_discord_id, autore_nome, tipo, titolo, descrizione)
            values (@did, @nome, @tipo, @titolo, @desc)
            returning id
            """, new { did = DiscordId(User)!, nome = NomeUtente(User), tipo, titolo, desc });

        InviaWebhook($"🎫 Nuovo ticket **#{id}** ({tipo}) da **{NomeUtente(User)}**: {titolo}");
        return Results.Ok(new { id });
    }

    [HttpPatch("/api/admin/ticket/{id:int}")]
    public async Task<IResult> Gestisci(int id, RichiestaGestisciTicket req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAdmin(User)) return Results.Forbid();

        var stato = (req.Stato ?? "").Trim().ToLowerInvariant();
        if (stato is not ("aperto" or "in_corso" or "risolto" or "respinto"))
            return Results.BadRequest(new { errore = "Stato non valido." });

        await using var c = await db.OpenConnectionAsync();
        await c.ExecuteAsync("""
            update fanta_torneo.ticket
            set stato = @stato,
                risposta_admin = @risposta,
                gestito_da = @admin,
                aggiornato_il = now()
            where id = @id
            """, new { id, stato, risposta = string.IsNullOrWhiteSpace(req.RispostaAdmin) ? null : req.RispostaAdmin.Trim(), admin = NomeUtente(User) });

        return Results.Ok(new { ok = true });
    }
}
