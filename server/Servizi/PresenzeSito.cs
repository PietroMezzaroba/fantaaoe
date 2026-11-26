namespace FantaAoe.Api;

public sealed record UtenteOnline(
    string IdDiscord,
    string Nome,
    string? Avatar,
    int? PlayerId,
    string? PlayerNick,
    string[] Ruoli,
    string Pagina,
    string[] Pagine,
    int Schede,
    DateTimeOffset ConnessoDal,
    DateTimeOffset UltimaAttivita,
    bool InAsta);

public sealed record PaginaOnline(
    string Pagina,
    int Totale,
    int Loggati,
    int Ospiti);

public sealed record IstantaneaOnline(
    int TotaleConnessioni,
    int TotalePersone,
    int Loggati,
    int Ospiti,
    UtenteOnline[] Utenti,
    PaginaOnline[] PerPagina,
    DateTimeOffset AggiornatoIl);

/// <summary>
/// Chi sta guardando il sito in questo momento (tutte le pagine, via SignalR /hubs/asta).
/// Ogni scheda aperta è una connessione: gli utenti connessi con Discord vengono raggruppati per id Discord
/// (mostrando quante schede hanno aperte e la pagina più recente), gli ospiti senza login si contano come anonimi.
/// L'istantanea viene inviata solo agli admin (gruppo "admin-online" o GET /api/admin/online).
/// </summary>
public sealed class PresenzeSito
{
    private sealed class SchedaConnessa
    {
        public required string IdConnessione { get; init; }
        public string? IdDiscord { get; init; }
        public string? Nome { get; init; }
        public string? Avatar { get; init; }
        public int? PlayerId { get; set; }
        public string? PlayerNick { get; set; }
        public required string[] Ruoli { get; init; }
        public string Pagina { get; set; } = "/";
        public DateTimeOffset ConnessoDal { get; init; }
        public DateTimeOffset UltimaAttivita { get; set; }
    }

    private readonly Dictionary<string, SchedaConnessa> connessioni = new();
    private readonly object blocco = new();

    private static readonly char[] DelimitatoriQuery = ['?', '#'];

    public static string NormalizzaPagina(string? percorso)
    {
        if (string.IsNullOrWhiteSpace(percorso)) return "/";
        var p = percorso.Trim();
        var taglio = p.IndexOfAny(DelimitatoriQuery);
        if (taglio >= 0) p = p[..taglio];
        if (!p.StartsWith('/')) return "/";
        if (p.Length > 100) p = p[..100];
        if (p.Length > 1 && p.EndsWith('/')) p = p.TrimEnd('/');
        return p.Length == 0 ? "/" : p;
    }

    public void Registra(string idConnessione, string? idDiscord, string? nome, string? avatar, string[] ruoli)
    {
        var adesso = DateTimeOffset.UtcNow;
        lock (blocco)
        {
            connessioni[idConnessione] = new SchedaConnessa
            {
                IdConnessione = idConnessione,
                IdDiscord = idDiscord,
                Nome = nome,
                Avatar = avatar,
                Ruoli = ruoli,
                Pagina = "/",
                ConnessoDal = adesso,
                UltimaAttivita = adesso,
            };
        }
    }

    /// <returns>true se la pagina della connessione è cambiata</returns>
    public bool ImpostaPagina(string idConnessione, string? percorso)
    {
        var pagina = NormalizzaPagina(percorso);
        lock (blocco)
        {
            if (!connessioni.TryGetValue(idConnessione, out var scheda)) return false;
            if (scheda.Pagina == pagina) return false;
            scheda.Pagina = pagina;
            scheda.UltimaAttivita = DateTimeOffset.UtcNow;
            return true;
        }
    }

    /// <summary>
    /// Aggiorna nick/playerId sulle connessioni attive di un utente (dopo lookup iniziale o modifica da admin).
    /// Non crea connessioni nuove se l'utente si è già disconnesso.
    /// </summary>
    public bool AggiornaProfilo(string idDiscord, int? playerId, string? playerNick)
    {
        var trovato = false;
        lock (blocco)
        {
            foreach (var s in connessioni.Values)
            {
                if (s.IdDiscord != idDiscord) continue;
                s.PlayerId = playerId;
                s.PlayerNick = playerNick;
                trovato = true;
            }
        }
        return trovato;
    }

    public bool Rimuovi(string idConnessione)
    {
        lock (blocco) return connessioni.Remove(idConnessione);
    }

    public bool RimuoviUtente(string idDiscord)
    {
        lock (blocco)
        {
            var daTogliere = connessioni.Values.Where(x => x.IdDiscord == idDiscord).Select(x => x.IdConnessione).ToList();
            foreach (var k in daTogliere) connessioni.Remove(k);
            return daTogliere.Count > 0;
        }
    }

    public IstantaneaOnline Istantanea(PresenzeAsta presenzeAsta)
    {
        var inAstaSet = presenzeAsta.Elenco().ToHashSet();
        lock (blocco)
        {
            var tutte = connessioni.Values.ToList();
            var utenti = tutte
                .Where(x => x.IdDiscord is not null)
                .GroupBy(x => x.IdDiscord!)
                .Select(g =>
                {
                    var recente = g.OrderByDescending(x => x.UltimaAttivita).First();
                    var pagine = g.OrderByDescending(x => x.UltimaAttivita).Select(x => x.Pagina).Distinct().ToArray();
                    return new UtenteOnline(
                        IdDiscord: g.Key,
                        Nome: recente.Nome ?? "Utente",
                        Avatar: recente.Avatar,
                        PlayerId: recente.PlayerId,
                        PlayerNick: recente.PlayerNick,
                        Ruoli: recente.Ruoli,
                        Pagina: recente.Pagina,
                        Pagine: pagine,
                        Schede: g.Count(),
                        ConnessoDal: g.Min(x => x.ConnessoDal),
                        UltimaAttivita: g.Max(x => x.UltimaAttivita),
                        InAsta: inAstaSet.Contains(g.Key));
                })
                .OrderByDescending(u => u.InAsta)
                .ThenByDescending(u => u.UltimaAttivita)
                .ThenBy(u => u.Nome, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var ospitiLista = tutte.Where(x => x.IdDiscord is null).ToList();

            var pagineLoggati = tutte
                .Where(x => x.IdDiscord is not null)
                .GroupBy(x => x.Pagina)
                .ToDictionary(g => g.Key, g => g.Select(x => x.IdDiscord!).Distinct().Count());
            var pagineOspiti = ospitiLista
                .GroupBy(x => x.Pagina)
                .ToDictionary(g => g.Key, g => g.Count());

            var perPagina = pagineLoggati.Keys
                .Union(pagineOspiti.Keys)
                .Select(p =>
                {
                    var l = pagineLoggati.GetValueOrDefault(p);
                    var o = pagineOspiti.GetValueOrDefault(p);
                    return new PaginaOnline(p, l + o, l, o);
                })
                .OrderByDescending(x => x.Totale)
                .ThenByDescending(x => x.Loggati)
                .ThenBy(x => x.Pagina, StringComparer.Ordinal)
                .ToArray();

            return new IstantaneaOnline(
                TotaleConnessioni: tutte.Count,
                TotalePersone: utenti.Length + ospitiLista.Count,
                Loggati: utenti.Length,
                Ospiti: ospitiLista.Count,
                Utenti: utenti,
                PerPagina: perPagina,
                AggiornatoIl: DateTimeOffset.UtcNow);
        }
    }
}
