namespace FantaAoe.Api;

// Modelli dello schema "fanta_torneo" (Dapper: snake_case -> PascalCase).

public sealed class UtenteDiscord
{
    public string DiscordId { get; set; } = "";
    public string DiscordNome { get; set; } = "";
    public string? DiscordAvatar { get; set; }
    public int? PlayerId { get; set; }
    public string? PlayerNick { get; set; }
    public string CollegatoDa { get; set; } = "auto";
    public bool Verificato { get; set; }
    public DateTime AggiornatoIl { get; set; }
}

public sealed class Squadra
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? CapitanoDiscordId { get; set; }
    public string CapitanoNome { get; set; } = "";
    public int BerluIniziali { get; set; }
    public int BerluResidui { get; set; }
    public int OrdineChiamata { get; set; }
    public string? CanaleDiscordUrl { get; set; }
    public int NGiocatori { get; set; }
}

public sealed class Iscritto
{
    public int Id { get; set; }
    public int? PlayerId { get; set; }
    public string? DiscordId { get; set; }
    public string Nick { get; set; } = "";
    public string Fascia { get; set; } = "C";
    public int? Seeding { get; set; }
    public int PrezzoBase { get; set; }
    public int? SquadraId { get; set; }
    public string? SquadraNome { get; set; }
    public int? PrezzoAsta { get; set; }
    public string? Note { get; set; }
    public int? EloInterno { get; set; }
    public int? Elo1v1 { get; set; }
    public int? EloTg { get; set; }
    public int? NDiti { get; set; }
    public double? Winrate { get; set; }
}

public sealed class DisponibilitaRiga
{
    public int Id { get; set; }
    public string DiscordId { get; set; } = "";
    public DateTime Data { get; set; }
    public string Stato { get; set; } = "disponibile";
    public string? FasciaOraria { get; set; }
    public string? Note { get; set; }
    public DateTime AggiornataIl { get; set; }
}

public sealed class TicketRiga
{
    public int Id { get; set; }
    public string AutoreDiscordId { get; set; } = "";
    public string AutoreNome { get; set; } = "";
    public string Tipo { get; set; } = "segnalazione";
    public string Titolo { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public string Stato { get; set; } = "aperto";
    public string? RispostaAdmin { get; set; }
    public string? GestitoDa { get; set; }
    public DateTime CreatoIl { get; set; }
    public DateTime AggiornatoIl { get; set; }
}

public sealed class AstaStatoRiga
{
    public int Id { get; set; } = 1;
    public string Stato { get; set; } = "attesa";
    public int? IscrittoId { get; set; }
    public int OffertaAttuale { get; set; }
    public int? SquadraMiglioreId { get; set; }
    public int? TurnoSquadraId { get; set; }
    public int RoundNumero { get; set; } = 1;
    public string? Messaggio { get; set; }
    public DateTime AggiornatoIl { get; set; }
}

public sealed class AstaOffertaRiga
{
    public int Id { get; set; }
    public int IscrittoId { get; set; }
    public int SquadraId { get; set; }
    public string SquadraNome { get; set; } = "";
    public int Importo { get; set; }
    public bool Annullata { get; set; }
    public string CreataDa { get; set; } = "";
    public DateTime CreataIl { get; set; }
}

// ---------- Request DTOs ----------

public sealed record RichiestaCollega(int? PlayerId, bool? Verificato);

public sealed record RichiestaIscritto(
    int? Id,
    int? PlayerId,
    string? DiscordId,
    string Nick,
    string Fascia,
    int? Seeding,
    int PrezzoBase,
    string? Note);

public sealed record RichiestaSquadra(
    int? Id,
    string Nome,
    string CapitanoNome,
    string? CapitanoDiscordId,
    int BerluIniziali,
    int OrdineChiamata,
    string? CanaleDiscordUrl);

public sealed record RichiestaDisponibilita(
    string Data,
    string Stato,
    string? FasciaOraria,
    string? Note);

public sealed record RichiestaTicket(
    string Tipo,
    string Titolo,
    string Descrizione);

public sealed record RichiestaGestisciTicket(
    string Stato,
    string? RispostaAdmin);

public sealed record RichiestaChiamaAsta(
    int IscrittoId,
    int? PrezzoPartenza);

public sealed record RichiestaStatoAsta(
    string Stato,
    string? Messaggio,
    int? TurnoSquadraId);

public sealed record RichiestaOffertaAsta(
    int Importo,
    int? SquadraId);
