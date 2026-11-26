using Microsoft.AspNetCore.SignalR;
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
/// Canale in tempo reale dell'asta (/hubs/asta). I client restano connessi (e dichiarano solo la presenza) e il server
/// invia "astaAggiornata" (nuova chiamata, offerta, acquisto...) e "astaPresenze" (chi è connesso), così non serve alcun polling.
/// </summary>
public sealed class AstaHub(PresenzeAsta presenze) : Hub
{
    public const string Percorso = "/hubs/asta";
    public const string EventoAggiornata = "astaAggiornata";
    public const string EventoPresenze = "astaPresenze";

    private string? Utente() => Context.User is { } u && EAutenticato(u) ? DiscordId(u) : null;

    // Chi si connette parte con l'elenco attuale dei presenti (lui non è ancora "presente": lo diventa solo se lo dichiara)
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(EventoPresenze, presenze.Elenco());
        await base.OnConnectedAsync();
    }

    // Il client dichiara se sta guardando la pagina con l'elenco dei capitani (entrando e uscendo da quella pagina)
    public async Task Presenza(bool presente)
    {
        var utente = Utente();
        if (utente is null) return;
        var cambiato = presente ? presenze.Aggiungi(Context.ConnectionId, utente) : presenze.Rimuovi(Context.ConnectionId);
        if (cambiato) await Clients.All.SendAsync(EventoPresenze, presenze.Elenco());
    }

    public override async Task OnDisconnectedAsync(Exception? eccezione)
    {
        if (presenze.Rimuovi(Context.ConnectionId))
            await Clients.All.SendAsync(EventoPresenze, presenze.Elenco());
        await base.OnDisconnectedAsync(eccezione);
    }
}
