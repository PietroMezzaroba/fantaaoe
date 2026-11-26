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
public class ProfiloController : ControllerBase
{
    private readonly NpgsqlDataSource? db;

    public ProfiloController(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    // Impostazioni dell'utente (audio, tema): salvate subito a ogni cambio, senza tasto di salvataggio
    [HttpPut("/api/profilo/impostazioni")]
    public async Task<IResult> SalvaImpostazioni(RichiestaImpostazioni req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();

        if (req.Cursore is not null && !CursoriValidi.Contains(req.Cursore))
            return Results.BadRequest(new { errore = "Cursore non valido" });

        await using var c = await db.OpenConnectionAsync();
        var righe = await c.ExecuteAsync("""
            update fanta_torneo.utenti_info
            set audio_attivo = coalesce(@audio, audio_attivo),
                layout_scuro = coalesce(@scuro, layout_scuro),
                cursore = coalesce(@cursore, cursore),
                animazione_iniziale = coalesce(@animazione, animazione_iniziale)
            where id_discord = @did
            """, new { audio = req.AudioAttivo, scuro = req.LayoutScuro, cursore = req.Cursore, animazione = req.AnimazioneIniziale, did = DiscordId(User)! });
        if (righe == 0) return Results.Problem("Utente non presente in utenti_info", statusCode: 404);
        return Results.Ok(new { ok = true });
    }

    [HttpGet("/api/profilo")]
    public async Task<IResult> Profilo()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        var did = DiscordId(User)!;
        await RegistraUtenteDiscord(db, User);

        await using var c = await db.OpenConnectionAsync();
        var legame = await c.QuerySingleOrDefaultAsync<UtenteDiscord>("""
            select u.discord_id, u.discord_nome, u.discord_avatar, u.player_id,
                   g.nick as player_nick, u.collegato_da, u.verificato, u.aggiornato_il
            from fanta_torneo.utenti_discord u
            left join fanta.giocatori g on g.player_id = u.player_id
            where u.discord_id = @did
            """, new { did });

        GiocatoreDettaglio? giocatore = null;
        IEnumerable<CivStat> civ = [];
        if (legame?.PlayerId is int pid)
        {
            giocatore = await c.QuerySingleOrDefaultAsync<GiocatoreDettaglio>(
                "select * from fanta.giocatori where player_id = @pid", new { pid });
            civ = await c.QueryAsync<CivStat>("""
                select pg.civ, count(*) as partite, sum(pg.vinto) as vittorie
                from fanta.partite_giocatori pg join fanta.partite p using (id_game)
                where pg.player_id = @pid and p.valida and pg.civ is not null
                group by pg.civ
                order by count(*) desc, sum(pg.vinto) desc
                limit 6
                """, new { pid });
        }

        var iscrizione = await c.QuerySingleOrDefaultAsync<Iscritto>("""
            select 0 as id, @pid as player_id, i.id_discord as discord_id, coalesce(i.nickname, '') as nick,
                   i.fascia, i.seeding, coalesce(i.prezzo_base, 0) as prezzo_base, i.id_squadra as squadra_id,
                   i.prezzo_asta, i.stato_validazione as stato, i.note
            from fanta_torneo.utenti_iscritti i
            where i.id_discord = @did
            """, new { did, pid = legame?.PlayerId });

        return Results.Ok(new { legame, giocatore, civ, iscrizione });
    }

    // Le iscrizioni sono aperte solo quando configurazioni.stato_globale_asta vale 'iscrizione'
    private static async Task<bool> IscrizioniAperte(NpgsqlConnection c) =>
        await c.ExecuteScalarAsync<string?>("select valore from fanta_torneo.configurazioni where chiave = 'stato_globale_asta'") == "iscrizione";

    // Stato dell'iscrizione per l'utente connesso: iscrizioni aperte? sono già iscritto?
    [HttpGet("/api/profilo/iscrizione")]
    public async Task<IResult> StatoIscrizione()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        var did = DiscordId(User)!;

        await using var c = await db.OpenConnectionAsync();
        var aperte = await IscrizioniAperte(c);
        var giaIscritto = await c.ExecuteScalarAsync<bool>("select exists(select 1 from fanta_torneo.utenti_iscritti where id_discord = @did)", new { did });
        return Results.Ok(new { aperte, giaIscritto });
    }

    // L'utente conferma la propria iscrizione: un solo record per utente in utenti_iscritti
    [HttpPost("/api/profilo/iscriviti")]
    public async Task<IResult> Iscriviti()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        var did = DiscordId(User)!;
        await RegistraUtenteDiscord(db, User);

        await using var c = await db.OpenConnectionAsync();
        if (!await IscrizioniAperte(c))
            return Results.Json(new { errore = "Le iscrizioni al Fanta Age sono disattive." }, statusCode: 403);
        var nick = NomeUtente(User);
        var inserite = await c.ExecuteAsync("""
            insert into fanta_torneo.utenti_iscritti (id_discord, id_squadra, nickname, fascia, seeding)
            values (@did, null, @nick, 'da_calcolare', 0)
            on conflict (id_discord) do nothing
            """, new { did, nick });
        if (inserite == 0)
            return Results.BadRequest(new { errore = "Risulti già iscritto al FantaAge." });

        InviaWebhook($"📝 Nuova iscrizione al FantaAge: **{nick}**");
        return Results.Ok(new { ok = true, stato_validazione = "in_validazione" });
    }

    [HttpPost("/api/profilo/collega")]
    public async Task<IResult> Collega(RichiestaCollega req)
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        var did = DiscordId(User)!;
        await RegistraUtenteDiscord(db, User);

        await using var c = await db.OpenConnectionAsync();
        if (req.PlayerId is int pid)
        {
            var esiste = await c.ExecuteScalarAsync<bool>(
                "select exists(select 1 from fanta.giocatori where player_id = @pid)", new { pid });
            if (!esiste) return Results.BadRequest(new { errore = "Giocatore non trovato in classifica." });
        }

        var verificato = EAdmin(User);
        await c.ExecuteAsync("""
            update fanta_torneo.utenti_discord
            set player_id = @pid,
                collegato_da = @chi,
                verificato = @verificato,
                aggiornato_il = now()
            where discord_id = @did
            """, new { pid = req.PlayerId, chi = verificato ? "admin" : "utente", verificato, did });

        return Results.Ok(new { ok = true });
    }

        // Il giocatore conferma il collegamento automatico proposto da Thomuggia
    [HttpPost("/api/profilo/conferma")]
    public async Task<IResult> Conferma()
    {
        if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
        if (!EAutenticato(User)) return Results.Unauthorized();
        var did = DiscordId(User)!;
        await using var c = await db.OpenConnectionAsync();
        var pid = await c.ExecuteScalarAsync<int?>(
            "select player_id from fanta_torneo.utenti_discord where discord_id = @did", new { did });
        if (pid is null) return Results.BadRequest(new { errore = "Nessun profilo da confermare." });

        await c.ExecuteAsync("""
            update fanta_torneo.utenti_discord
            set verificato = true,
                collegato_da = 'utente_confermato',
                aggiornato_il = now()
            where discord_id = @did
            """, new { did });


        return Results.Ok(new { ok = true });
    }
}
