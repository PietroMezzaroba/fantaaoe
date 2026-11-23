namespace FantaAoe.Api;

// Righe lette dalle viste dello schema "fanta" (Dapper: colonne snake_case → proprietà PascalCase).

public sealed class Giocatore
{
    public int PlayerId { get; set; }
    public string Nick { get; set; } = "";
    public string? Clan { get; set; }
    public int EloInterno { get; set; }
    public int? Elo1v1 { get; set; }
    public int? EloTg { get; set; }
    public int NDiti { get; set; }
    public int NDitiVinti { get; set; }
    public double Winrate { get; set; }
    public DateTime? UltimoDito { get; set; }
    public long NAccount { get; set; }
}

public sealed class GiocatoreDettaglio
{
    public int PlayerId { get; set; }
    public string Nick { get; set; } = "";
    public string? Clan { get; set; }
    public int EloInterno { get; set; }
    public double EloExact { get; set; }
    public int NDiti { get; set; }
    public int NDitiVinti { get; set; }
    public double Winrate { get; set; }
    public DateTime? UltimoDito { get; set; }
    public int? Elo1v1 { get; set; }
    public long? Account1v1 { get; set; }
    public DateTime? UltimaRanked1v1 { get; set; }
    public int? EloTg { get; set; }
    public long? AccountTg { get; set; }
    public DateTime? UltimaRankedTg { get; set; }
    public int? Elo1v1MassimoStorico { get; set; }
    public int? EloTgMassimoStorico { get; set; }
    public long NAccount { get; set; }
}

public sealed class Account
{
    public long Aoe2Id { get; set; }
    public string? SteamName { get; set; }
    public string? Country { get; set; }
    public int? Mmr1v1 { get; set; }
    public int? Max1v1Mmr { get; set; }
    public int? MmrTg { get; set; }
    public int? MaxTgMmr { get; set; }
    public int? Games1v1 { get; set; }
    public int? GamesTg { get; set; }
    public DateTime? LastGame1v1 { get; set; }
    public DateTime? LastGameTg { get; set; }
    public string? Status { get; set; }
}

public sealed class PartitaDelGiocatore
{
    public long IdGame { get; set; }
    public DateTime? StartedAt { get; set; }
    public int DurataInGameMinuti { get; set; }
    public int? Dito { get; set; }
    public int Vinto { get; set; }
    public string? Civ { get; set; }
    public double? EloDelta { get; set; }
    public double? EloAfter { get; set; }
}

public sealed class PuntoElo
{
    public DateTime? Data { get; set; }
    public double Elo { get; set; }
}

public sealed class CivStat
{
    public string Civ { get; set; } = "";
    public long Partite { get; set; }
    public long Vittorie { get; set; }
}

public sealed class Statistiche
{
    public long GiocatoriTotali { get; set; }
    public long Attivi90 { get; set; }
    public long Attivi30 { get; set; }
    public long PartiteValide { get; set; }
    public long Partite30 { get; set; }
    public long Partite7 { get; set; }
    public long MinutiTotali { get; set; }
    public int? UltimoDito { get; set; }
    public DateTime? PrimaPartita { get; set; }
}

public sealed class Settimana
{
    public DateTime Inizio { get; set; }
    public long Partite { get; set; }
}

public sealed class Partita
{
    public long IdGame { get; set; }
    public DateTime? StartedAt { get; set; }
    public int DurataInGameMinuti { get; set; }
    public int DurataIrlMinuti { get; set; }
    public string? MapName { get; set; }
    public bool Valida { get; set; }
    public int? Dito { get; set; }
}

public sealed class GiocatoreInPartita
{
    public long IdGame { get; set; }
    public long Aoe2Id { get; set; }
    public int? PlayerId { get; set; }
    public string? Nick { get; set; }
    public int Vinto { get; set; }
    public string? Civ { get; set; }
    public double? EloBefore { get; set; }
    public double? EloDelta { get; set; }
    public double? EloAfter { get; set; }
}
