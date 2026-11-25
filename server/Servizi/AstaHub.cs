using Microsoft.AspNetCore.SignalR;

namespace FantaAoe.Api;

/// <summary>
/// Canale in tempo reale dell'asta (/hubs/asta). I client non chiamano metodi: restano connessi e il server
/// invia l'evento "astaAggiornata" quando succede qualcosa (nuova chiamata o nuova offerta), così non serve alcun polling.
/// </summary>
public sealed class AstaHub : Hub
{
    public const string Percorso = "/hubs/asta";
    public const string EventoAggiornata = "astaAggiornata";
}
