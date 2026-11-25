using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;

namespace FantaAoe.Api;

internal static class TorneoUtil
{
    internal static readonly HttpClient WebhookHttp = new() { Timeout = TimeSpan.FromSeconds(3) };

    internal static void InviaWebhook(string messaggio)
    {
        var url = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_STAFF");
        if (string.IsNullOrWhiteSpace(url)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(new { content = messaggio }),
                    Encoding.UTF8,
                    "application/json");
                await WebhookHttp.PostAsync(url, content);
            }
            catch { /* mai bloccare l'app se Discord non risponde */ }
        });
    }

    internal sealed class IdElo { public int Id { get; set; } public int Elo { get; set; } }
    internal sealed class OffertaPrec { public int SquadraId { get; set; } public int Importo { get; set; } }

    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal static string? DiscordId(ClaimsPrincipal u) => u.FindFirst("discord_id")?.Value;
    internal static string NomeUtente(ClaimsPrincipal u) => u.FindFirst("nome")?.Value ?? "Utente";
    internal static string? AvatarUtente(ClaimsPrincipal u) => u.FindFirst("avatar")?.Value;
    internal static bool EAdmin(ClaimsPrincipal u) => u.Identity?.IsAuthenticated == true && u.IsInRole(Accesso.Admin);
    internal static bool ECapitano(ClaimsPrincipal u) => u.Identity?.IsAuthenticated == true && (u.IsInRole(Accesso.Capitano) || u.IsInRole(Accesso.Admin));
    internal static bool EAutenticato(ClaimsPrincipal u) => u.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(DiscordId(u));

    internal sealed class ImpostazioniUtente { public bool AudioAttivo { get; set; } public bool LayoutScuro { get; set; } public string Cursore { get; set; } = CursorePredefinito; public bool AnimazioneIniziale { get; set; } = true; }

    internal const string CursorePredefinito = "accattivante";
    internal static readonly string[] CursoriValidi = ["accattivante", "classico"];

    // Impostazioni dell'utente (audio, tema) da utenti_info; null se non ci sono o la tabella non è disponibile.
    internal static async Task<ImpostazioniUtente?> LeggiImpostazioni(NpgsqlDataSource? db, ClaimsPrincipal u)
    {
        if (db is null || !EAutenticato(u)) return null;
        try
        {
            await using var c = await db.OpenConnectionAsync();
            return await c.QuerySingleOrDefaultAsync<ImpostazioniUtente>(
                "select audio_attivo, layout_scuro, cursore, animazione_iniziale from fanta_torneo.utenti_info where id_discord = @did",
                new { did = DiscordId(u)! });
        }
        catch
        {
            return null;
        }
    }

    // I ruoli di un utente stanno in fanta_torneo.utenti_ruoli (più righe = più ruoli); "nessun ruolo" = nessuna riga.
    // Registra l'utente (utenti_discord, utenti_info) e ne restituisce i ruoli letti dal database.
    //  - al primo accesso i ruoli di partenza sono quelli ricavati da Discord (ruoliDiscord) e vengono salvati in utenti_ruoli;
    //  - dopo il primo accesso decide solo il database; l'unica eccezione è "admin" ricavato da Discord (amministratore del server o
    //    ADMIN_UTENTI), che viene sempre garantito per non restare chiusi fuori.
    internal static async Task<List<string>> RegistraEAssegna(NpgsqlDataSource db, string did, string nome, string? avatar, IReadOnlyCollection<string> ruoliDiscord)
    {
        await using var c = await db.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        await c.ExecuteAsync("""
            insert into fanta_torneo.utenti_discord (discord_id, discord_nome, discord_avatar, aggiornato_il)
            values (@did, @nome, @avatar, now())
            on conflict (discord_id) do update
            set discord_nome = excluded.discord_nome,
                discord_avatar = excluded.discord_avatar,
                aggiornato_il = now()
            """, new { did, nome, avatar }, tx);
        var nuovo = await c.ExecuteAsync("""
            insert into fanta_torneo.utenti_info (id_discord)
            values (@did)
            on conflict (id_discord) do nothing
            """, new { did }, tx) == 1;

        var daAssegnare = nuovo ? ruoliDiscord : ruoliDiscord.Where(r => r == Accesso.Admin).ToList();
        foreach (var chiave in daAssegnare.Distinct())
        {
            await c.ExecuteAsync("""
                insert into fanta_torneo.utenti_ruoli (id_utente, id_ruolo)
                select @did, id_ruolo from fanta_torneo.ruoli where chiave_ruolo = @chiave
                on conflict (id_utente, id_ruolo) do nothing
                """, new { did, chiave }, tx);
        }

        var ruoli = (await c.QueryAsync<string>("""
            select r.chiave_ruolo
            from fanta_torneo.utenti_ruoli ur
            join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
            where ur.id_utente = @did
            order by r.id_ruolo
            """, new { did }, tx)).ToList();
        await tx.CommitAsync();
        return ruoli;
    }

    internal static async Task RegistraUtenteDiscord(NpgsqlDataSource? db, ClaimsPrincipal u)
    {
        if (db is null || !EAutenticato(u)) return;
        try
        {
            var daClaim = u.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            await RegistraEAssegna(db, DiscordId(u)!, NomeUtente(u), AvatarUtente(u), daClaim);
        }
        catch
        {
            // non bloccare il login se il DB torneo non è temporaneamente disponibile
        }
    }
}
