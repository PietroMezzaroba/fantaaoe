using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FantaAoe.Api;

// Broadcaster in-memory per Server-Sent Events (SSE) dell'asta dal vivo.
// Aggiorna in tempo reale pannello Admin, Capitani e Overlay OBS senza dipendenze esterne.
public sealed class AstaHub
{
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clienti = new();

    public (Guid Id, ChannelReader<string> Reader) Iscriviti()
    {
        var id = Guid.NewGuid();
        var ch = Channel.CreateBounded<string>(new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        _clienti[id] = ch;
        return (id, ch.Reader);
    }

    public void Disiscriviti(Guid id)
    {
        if (_clienti.TryRemove(id, out var ch))
            ch.Writer.TryComplete();
    }

    public void Notifica(string payloadJson)
    {
        foreach (var kv in _clienti)
            kv.Value.Writer.TryWrite(payloadJson);
    }
}
