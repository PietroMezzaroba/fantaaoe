using Dapper;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using FantaAoe.Api.Controllers;

namespace FantaAoe.Api;

/// <summary>
/// Timer dell'asta: da quando c'è una prima puntata, ogni offerta lo riporta al valore iniziale (configurazioni.timer_asta_secondi);
/// quando scade, l'offerta si chiude da sola come se lo streamer avesse premuto "Termina offerta".
/// La scadenza è salvata nel database (configurazioni.scadenza_asta), così sopravvive a un riavvio del server.
/// </summary>
public sealed class TimerAsta(NpgsqlDataSource? db, IHubContext<AstaHub> hub, ILogger<TimerAsta> log) : IHostedService
{
    private CancellationTokenSource? attesa;
    private readonly object blocco = new();

    // All'avvio del server riprende il timer di un'offerta rimasta in corso
    public async Task StartAsync(CancellationToken ct)
    {
        if (db is null) return;
        try
        {
            await using var c = await db.OpenConnectionAsync(ct);
            var scadenzaMs = await c.ExecuteScalarAsync<long?>("""
                select (extract(epoch from s.valore::timestamptz) * 1000)::bigint
                from fanta_torneo.configurazioni s
                join fanta_torneo.configurazioni f on f.chiave = 'fase_flusso_asta' and f.valore = 'fase di acquisto'
                where s.chiave = 'scadenza_asta'
                """);
            if (scadenzaMs is { } ms) Pianifica(DateTimeOffset.FromUnixTimeMilliseconds(ms));
        }
        catch (Exception e)
        {
            log.LogWarning(e, "Timer dell'asta: impossibile riprendere la scadenza all'avvio");
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        Annulla();
        return Task.CompletedTask;
    }

    /// <summary>Fissa (o sposta) la scadenza: alla scadenza l'offerta si chiude da sola.</summary>
    public void Pianifica(DateTimeOffset scadenza)
    {
        CancellationToken token;
        lock (blocco)
        {
            attesa?.Cancel();
            attesa = new CancellationTokenSource();
            token = attesa.Token;
        }
        _ = Attendi(scadenza, token);
    }

    /// <summary>Spegne il timer (offerta chiusa a mano o conclusa).</summary>
    public void Annulla()
    {
        lock (blocco)
        {
            attesa?.Cancel();
            attesa = null;
        }
    }

    private async Task Attendi(DateTimeOffset scadenza, CancellationToken token)
    {
        try
        {
            var ritardo = scadenza - DateTimeOffset.UtcNow;
            if (ritardo > TimeSpan.Zero) await Task.Delay(ritardo, token);
            if (token.IsCancellationRequested) return;
            await Scatta();
        }
        catch (OperationCanceledException)
        {
            // timer spostato o spento: nessuna chiusura
        }
        catch (Exception e)
        {
            log.LogError(e, "Timer dell'asta: errore alla scadenza");
        }
    }

    private async Task Scatta()
    {
        if (db is null) return;
        await using var c = await db.OpenConnectionAsync();
        var errore = await AstaController.ChiudiOfferta(c, soloSeScaduta: true);
        if (errore is null)
            await AstaController.Notifica(hub, c, "acquisto");
        // altrimenti: nessuna offerta da chiudere, o qualcuno ha rilanciato (e ha già riprogrammato il timer)
    }
}
