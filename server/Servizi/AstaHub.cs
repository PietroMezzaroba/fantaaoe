using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using static FantaAoe.Api.TorneoUtil;

namespace FantaAoe.Api;

/// <summary>
/// Chi è sulla pagina dell'asta con l'elenco dei capitani in questo momento. Si tiene per connessione: una persona con più
/// schede aperte resta presente finché ne ha almeno una; una connessione che cade (o una scheda chiusa) toglie la sua presenza.
/// </summary>
public sealed class PresenzeAsta
{
    private readonly Dictionary<string, string> utentePerConnessione = new();
    private readonly object blocco = new();

    /// <returns>true se l'elenco dei presenti è cambiato (l'utente prima non c'era)</returns>
    public bool Aggiungi(string idConnessione, string idDiscord)
    {
        lock (blocco)
        {
            if (utentePerConnessione.TryGetValue(idConnessione, out var gia) && gia == idDiscord) return false;
            var primaPresente = utentePerConnessione.ContainsValue(idDiscord);
            utentePerConnessione[idConnessione] = idDiscord;
            return !primaPresente;
        }
    }

    /// <returns>true se l'elenco dei presenti è cambiato (l'utente non ha più nessuna connessione presente)</returns>
    public bool Rimuovi(string idConnessione)
    {
        lock (blocco)
        {
            if (!utentePerConnessione.Remove(idConnessione, out var utente)) return false;
            return !utentePerConnessione.ContainsValue(utente);
        }
    }

    /// <returns>true se l'utente era presente (tutte le sue connessioni vengono tolte)</returns>
    public bool RimuoviUtente(string idDiscord)
    {
        lock (blocco)
        {
            var connessioni = utentePerConnessione.Where(x => x.Value == idDiscord).Select(x => x.Key).ToList();
            foreach (var k in connessioni) utentePerConnessione.Remove(k);
            return connessioni.Count > 0;
        }
    }

    public string[] Elenco()
    {
        lock (blocco) return utentePerConnessione.Values.Distinct().ToArray();
    }
}

/// <summary>
/// Canale in tempo reale (/hubs/asta):
///  - "astaAggiornata" (nuova chiamata, offerta, acquisto...) e "astaPresenze" (capitani sulla pagina dell'asta) a tutti;
///  - "sitoOnline" (chi è online sul sito e su quale pagina) solo al gruppo "admin-online", verificato lato server.
/// </summary>
public sealed class AstaHub : Hub
{
    public const string Percorso = "/hubs/asta";
    public const string EventoAggiornata = "astaAggiornata";
    public const string EventoPresenze = "astaPresenze";
    public const string EventoOnline = "sitoOnline";
    public const string EventoRuoliAggiornati = "ruoliAggiornati";
    public const string GruppoAdminOnline = "admin-online";

    private sealed class LegameGiocatore
    {
        public int? PlayerId { get; set; }
        public string? Nick { get; set; }
    }

    private readonly PresenzeAsta presenze;
    private readonly PresenzeSito sito;
    private readonly CacheRuoli cacheRuoli;
    private readonly NpgsqlDataSource? db;

    public AstaHub(PresenzeAsta presenze, PresenzeSito sito, CacheRuoli cacheRuoli, NpgsqlDataSource? db = null)
    {
        this.presenze = presenze;
        this.sito = sito;
        this.cacheRuoli = cacheRuoli;
        this.db = db;
    }

    private string? Utente() => Context.User is { } u && EAutenticato(u) ? DiscordId(u) : null;

    private Task InviaOnlineAgliAdmin() =>
        Clients.Group(GruppoAdminOnline).SendAsync(EventoOnline, sito.Istantanea(presenze));

    // Chi si connette riceve subito le presenze dell'asta e viene registrato tra chi è online sul sito
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(EventoPresenze, presenze.Elenco());

        var user = Context.User;
        var did = user is not null && EAutenticato(user) ? DiscordId(user) : null;
        var nome = did is not null ? NomeUtente(user!) : null;
        var avatar = did is not null ? AvatarUtente(user!) : null;
        var ruoli = did is not null
            ? user!.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray()
            : Array.Empty<string>();

        sito.Registra(Context.ConnectionId, did, nome, avatar, ruoli);
        await InviaOnlineAgliAdmin();

        if (did is not null && db is not null)
        {
            try
            {
                await using var c = await db.OpenConnectionAsync();
                var legame = await c.QuerySingleOrDefaultAsync<LegameGiocatore>("""
                    select u.player_id, g.nick
                    from fanta_torneo.utenti_discord u
                    left join fanta.giocatori g on g.player_id = u.player_id
                    where u.discord_id = @did
                    """, new { did });
                if (legame?.PlayerId is not null && sito.AggiornaProfilo(did, legame.PlayerId, legame.Nick))
                    await InviaOnlineAgliAdmin();
            }
            catch
            {
                // mai bloccare il collegamento se il database non risponde
            }
        }

        await base.OnConnectedAsync();
    }

    // Il client comunica la pagina del sito che sta guardando (es. "/", "/asta", "/io")
    public async Task Pagina(string? percorso)
    {
        if (sito.ImpostaPagina(Context.ConnectionId, percorso))
            await InviaOnlineAgliAdmin();
    }

    // Solo gli admin possono iscriversi agli aggiornamenti in tempo reale di chi è online sul sito
    // (controlla i ruoli vivi dal DB, così un utente appena promosso admin può seguire subito senza riconnettere il WebSocket)
    public async Task SeguiOnline(bool attivo)
    {
        var did = Utente();
        if (did is null) return;
        var ruoliVivi = await cacheRuoli.Leggi(did);
        var admin = ruoliVivi is not null
            ? ruoliVivi.Contains(Accesso.Admin)
            : (Context.User is { } u && EAdmin(u));
        if (!admin) return;
        if (attivo)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GruppoAdminOnline);
            await Clients.Caller.SendAsync(EventoOnline, sito.Istantanea(presenze));
        }
        else
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GruppoAdminOnline);
        }
    }

    // Il client dichiara se sta guardando la pagina con l'elenco dei capitani (entrando e uscendo da quella pagina)
    public async Task Presenza(bool presente)
    {
        var utente = Utente();
        if (utente is null) return;
        var cambiato = presente ? presenze.Aggiungi(Context.ConnectionId, utente) : presenze.Rimuovi(Context.ConnectionId);
        if (cambiato)
        {
            await Clients.All.SendAsync(EventoPresenze, presenze.Elenco());
            await InviaOnlineAgliAdmin();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? eccezione)
    {
        var cambiatoAsta = presenze.Rimuovi(Context.ConnectionId);
        var cambiatoSito = sito.Rimuovi(Context.ConnectionId);
        if (cambiatoAsta)
            await Clients.All.SendAsync(EventoPresenze, presenze.Elenco());
        if (cambiatoSito || cambiatoAsta)
            await InviaOnlineAgliAdmin();
        await base.OnDisconnectedAsync(eccezione);
    }
}
