// API del fantaAoE. Regole del server: HTTP sulla 8080 (l'HTTPS è davanti), niente file su disco,
// configurazione e segreti da variabili d'ambiente.
// Dati: database della classifica, schema "fanta", utente in sola lettura (ConnectionStrings__Aoe).

using Dapper;
using FantaAoe.Api;
using Npgsql;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

var connessione = builder.Configuration.GetConnectionString("Aoe");
NpgsqlDataSource? db = string.IsNullOrWhiteSpace(connessione) ? null : NpgsqlDataSource.Create(connessione);

builder.Services.AddOutputCache(o =>
    o.AddPolicy("breve", p => p.Expire(TimeSpan.FromSeconds(60))));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseOutputCache();

// ---------- salute e info ----------

app.MapGet("/api/salute", async () =>
{
    var dbOk = false;
    if (db is not null)
    {
        try
        {
            await using var c = await db.OpenConnectionAsync();
            dbOk = await c.ExecuteScalarAsync<int>("select 1") == 1;
        }
        catch { dbOk = false; }
    }
    return Results.Ok(new
    {
        stato = dbOk ? "ok" : "db non raggiungibile",
        versione = Environment.GetEnvironmentVariable("VERSIONE") ?? "locale",
        ora = DateTime.UtcNow,
    });
});

var api = app.MapGroup("/api").CacheOutput("breve");

api.MapGet("/info", () => Results.Ok(new
{
    handbook = Environment.GetEnvironmentVariable("HANDBOOK_URL"),
}));

// ---------- giocatori ----------

api.MapGet("/giocatori", async (bool? tutti) =>
{
    if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
    await using var c = await db.OpenConnectionAsync();
    var righe = await c.QueryAsync<Giocatore>("""
        select player_id, nick, clan, elo_interno, elo_1v1, elo_tg,
               n_diti, n_diti_vinti, winrate, ultimo_dito, n_account
        from giocatori
        where @tutti or ultimo_dito > now() - interval '90 days'
        order by elo_interno desc, nick
        """, new { tutti = tutti ?? false });
    return Results.Ok(righe);
});

api.MapGet("/giocatori/{id:int}", async (int id) =>
{
    if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
    await using var c = await db.OpenConnectionAsync();

    var g = await c.QuerySingleOrDefaultAsync<GiocatoreDettaglio>(
        "select * from giocatori where player_id = @id", new { id });
    if (g is null) return Results.NotFound();

    var account = await c.QueryAsync<Account>("""
        select aoe2_id, steam_name, country, mmr_1v1, max_1v1_mmr, mmr_tg, max_tg_mmr,
               games_1v1, games_tg, last_game_1v1, last_game_tg, status
        from account where player_id = @id
        order by greatest(coalesce(mmr_1v1, 0), coalesce(mmr_tg, 0)) desc
        """, new { id });

    var partite = await c.QueryAsync<PartitaDelGiocatore>("""
        select p.id_game, p.started_at, p.durata_in_game_minuti, p.dito,
               pg.vinto, pg.civ, pg.elo_delta, pg.elo_after
        from partite_giocatori pg join partite p using (id_game)
        where pg.player_id = @id
        order by p.started_at desc nulls last, p.id_game desc
        limit 30
        """, new { id });

    var andamento = await c.QueryAsync<PuntoElo>("""
        select p.started_at as data, pg.elo_after as elo
        from partite_giocatori pg join partite p using (id_game)
        where pg.player_id = @id and p.valida and pg.elo_after is not null
        order by p.started_at nulls first, p.id_game
        """, new { id });

    var civ = await c.QueryAsync<CivStat>("""
        select pg.civ, count(*) as partite, sum(pg.vinto) as vittorie
        from partite_giocatori pg join partite p using (id_game)
        where pg.player_id = @id and p.valida and pg.civ is not null
        group by pg.civ
        order by count(*) desc, sum(pg.vinto) desc
        limit 12
        """, new { id });

    return Results.Ok(new { giocatore = g, account, partite, andamento, civ });
});

// ---------- partite ----------

api.MapGet("/partite", async (int? pagina, int? perPagina) =>
{
    if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
    var n = Math.Clamp(perPagina ?? 30, 1, 100);
    var p = Math.Max(pagina ?? 1, 1);
    await using var c = await db.OpenConnectionAsync();

    var partite = (await c.QueryAsync<Partita>("""
        select id_game, started_at, durata_in_game_minuti, durata_irl_minuti, map_name, valida, dito
        from partite
        order by started_at desc nulls last, id_game desc
        limit @n offset @salta
        """, new { n, salta = (p - 1) * n })).ToList();

    var ids = partite.Select(x => x.IdGame).ToArray();
    var giocatori = (await c.QueryAsync<GiocatoreInPartita>("""
        select id_game, aoe2_id, player_id, nick, vinto, civ, elo_before, elo_delta, elo_after
        from partite_giocatori where id_game = any(@ids)
        order by elo_before desc nulls last
        """, new { ids })).ToLookup(x => x.IdGame);

    var totale = await c.ExecuteScalarAsync<long>("select count(*) from partite");

    return Results.Ok(new
    {
        pagina = p,
        perPagina = n,
        totale,
        partite = partite.Select(x => new { partita = x, giocatori = giocatori[x.IdGame] }),
    });
});

api.MapGet("/partite/{id:long}", async (long id) =>
{
    if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
    await using var c = await db.OpenConnectionAsync();
    var partita = await c.QuerySingleOrDefaultAsync<Partita>("""
        select id_game, started_at, durata_in_game_minuti, durata_irl_minuti, map_name, valida, dito
        from partite where id_game = @id
        """, new { id });
    if (partita is null) return Results.NotFound();
    var giocatori = await c.QueryAsync<GiocatoreInPartita>("""
        select id_game, aoe2_id, player_id, nick, vinto, civ, elo_before, elo_delta, elo_after
        from partite_giocatori where id_game = @id
        order by vinto desc, elo_before desc nulls last
        """, new { id });
    return Results.Ok(new { partita, giocatori });
});

// ---------- civiltà ----------

api.MapGet("/civ", async (int? giorni) =>
{
    if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
    await using var c = await db.OpenConnectionAsync();
    var righe = await c.QueryAsync<CivStat>("""
        select pg.civ, count(*) as partite, sum(pg.vinto) as vittorie
        from partite_giocatori pg join partite p using (id_game)
        where p.valida and pg.civ is not null
          and (cast(@giorni as int) is null or p.started_at > now() - make_interval(days => cast(@giorni as int)))
        group by pg.civ
        order by count(*) desc
        """, new { giorni });
    return Results.Ok(righe);
});

// tutte le altre rotte non /api vanno alla SPA
app.MapFallbackToFile("index.html");

app.Run();
