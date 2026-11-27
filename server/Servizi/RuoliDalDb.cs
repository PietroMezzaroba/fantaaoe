using System.Collections.Concurrent;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace FantaAoe.Api;

/// <summary>
/// Cache breve (10 s) dei ruoli letti da fanta_torneo.utenti_ruoli per evitare query ripetute a ogni richiesta,
/// ma permettendo ai cambi di ruolo (da /admin o dalla sincronizzazione Discord -> DB) di avere effetto subito
/// senza che l'utente debba fare logout e login.
/// </summary>
public sealed class CacheRuoli
{
    private sealed record Voce(string[] Ruoli, DateTimeOffset ScadeIl);

    private static readonly TimeSpan Durata = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<string, Voce> voci = new();
    private readonly NpgsqlDataSource? db;

    public CacheRuoli(NpgsqlDataSource? db = null)
    {
        this.db = db;
    }

    public async Task<string[]?> Leggi(string discordId, CancellationToken ct = default)
    {
        var adesso = DateTimeOffset.UtcNow;
        if (voci.TryGetValue(discordId, out var voce) && voce.ScadeIl > adesso)
            return voce.Ruoli;
        if (db is null) return null;

        try
        {
            await using var c = await db.OpenConnectionAsync(ct);
            var ruoli = (await c.QueryAsync<string>("""
                select r.chiave_ruolo
                from fanta_torneo.utenti_ruoli ur
                join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                where ur.id_utente = @did
                order by r.id_ruolo
                """, new { did = discordId })).ToArray();
            voci[discordId] = new Voce(ruoli, adesso + Durata);
            return ruoli;
        }
        catch
        {
            // se il DB non risponde si mantiene il fallback del cookie senza buttare fuori nessuno
            return null;
        }
    }

    public void Imposta(string discordId, string[] ruoli) =>
        voci[discordId] = new Voce(ruoli, DateTimeOffset.UtcNow + Durata);

    public void Invalida(string discordId) => voci.TryRemove(discordId, out _);
}

/// <summary>
/// Sostituisce i ClaimTypes.Role del cookie di sessione con i ruoli vivi letti dal database (CacheRuoli).
/// Così un utente già connesso ottiene o perde ruoli (capitano, streamer, admin...) senza rifare il login.
/// </summary>
public sealed class TrasformaRuoliDalDb(CacheRuoli cache) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity id || !id.IsAuthenticated) return principal;
        var did = id.FindFirst("discord_id")?.Value;
        if (string.IsNullOrEmpty(did)) return principal;

        var ruoliDb = await cache.Leggi(did);
        if (ruoliDb is null) return principal;

        var clone = principal.Clone();
        if (clone.Identity is not ClaimsIdentity cid) return principal;
        foreach (var vecchio in cid.FindAll(ClaimTypes.Role).ToList())
            cid.RemoveClaim(vecchio);
        foreach (var r in ruoliDb)
            cid.AddClaim(new Claim(ClaimTypes.Role, r));
        return clone;
    }
}

/// <summary>
/// Controlla ogni 15 secondi i ruoli nel DB per i soli utenti attualmente connessi al sito (0 query se non c'è nessuno).
/// Se un ruolo cambia dall'esterno (es. sincronizzazione Discord -> DB), invalida la cache, aggiorna la dashboard
/// "Persone online" e avvisa il browser dell'utente via SignalR ("ruoliAggiornati") senza bisogno di ricaricare.
/// </summary>
public sealed class OsservatoreRuoli : BackgroundService
{
    private sealed class RigaRuoliUtente
    {
        public string IdUtente { get; set; } = "";
        public string[]? Ruoli { get; set; }
    }

    private readonly PresenzeSito sito;
    private readonly PresenzeAsta presenzeAsta;
    private readonly CacheRuoli cache;
    private readonly IHubContext<AstaHub> hub;
    private readonly ILogger<OsservatoreRuoli> log;
    private readonly NpgsqlDataSource? db;

    public OsservatoreRuoli(
        PresenzeSito sito,
        PresenzeAsta presenzeAsta,
        CacheRuoli cache,
        IHubContext<AstaHub> hub,
        ILogger<OsservatoreRuoli> log,
        NpgsqlDataSource? db = null)
    {
        this.sito = sito;
        this.presenzeAsta = presenzeAsta;
        this.cache = cache;
        this.hub = hub;
        this.log = log;
        this.db = db;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (db is null) continue;
            var online = sito.UtentiConRuoli();
            if (online.Count == 0) continue;

            try
            {
                var ids = online.Keys.ToArray();
                await using var c = await db.OpenConnectionAsync(stoppingToken);
                var righe = await c.QueryAsync<RigaRuoliUtente>("""
                    select ur.id_utente as id_utente, array_agg(r.chiave_ruolo order by r.id_ruolo) as ruoli
                    from fanta_torneo.utenti_ruoli ur
                    join fanta_torneo.ruoli r on r.id_ruolo = ur.id_ruolo
                    where ur.id_utente = any(@ids)
                    group by ur.id_utente
                    """, new { ids });
                var mappaDb = righe.ToDictionary(x => x.IdUtente, x => x.Ruoli ?? Array.Empty<string>());

                var cambiatoAlmenoUno = false;
                foreach (var (did, ruoliAttuali) in online)
                {
                    var nuovi = mappaDb.GetValueOrDefault(did, Array.Empty<string>());
                    if (StessiRuoli(ruoliAttuali, nuovi)) continue;

                    cache.Imposta(did, nuovi);
                    var primaEraAdmin = ruoliAttuali.Contains(Accesso.Admin);
                    var oraEAdmin = nuovi.Contains(Accesso.Admin);
                    sito.AggiornaRuoli(did, nuovi);
                    cambiatoAlmenoUno = true;

                    var conns = sito.ConnessioniDiUtente(did);
                    if (conns.Length > 0)
                    {
                        if (primaEraAdmin && !oraEAdmin)
                        {
                            foreach (var cid in conns)
                                await hub.Groups.RemoveFromGroupAsync(cid, AstaHub.GruppoAdminOnline, stoppingToken);
                        }
                        await hub.Clients.Clients(conns).SendAsync(AstaHub.EventoRuoliAggiornati, nuovi, stoppingToken);
                    }
                }

                if (cambiatoAlmenoUno)
                    await hub.Clients.Group(AstaHub.GruppoAdminOnline).SendAsync(AstaHub.EventoOnline, sito.Istantanea(presenzeAsta), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                log.LogWarning(e, "OsservatoreRuoli: impossibile verificare i ruoli dal database");
            }
        }
    }

    internal static bool StessiRuoli(string[] a, string[] b)
    {
        if (a.Length != b.Length) return false;
        var setA = a.ToHashSet();
        return b.All(setA.Contains);
    }
}
