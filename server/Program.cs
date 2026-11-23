// API del fantaAoE. Regole del server: HTTP sulla 8080 (l'HTTPS è davanti), niente file su disco,
// configurazione e segreti da variabili d'ambiente.
// Dati: database della classifica, schema "fanta", utente in sola lettura (ConnectionStrings__Aoe).

using System.Security.Claims;
using Dapper;
using FantaAoe.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

var connessione = builder.Configuration.GetConnectionString("Aoe");
NpgsqlDataSource? db = string.IsNullOrWhiteSpace(connessione) ? null : NpgsqlDataSource.Create(connessione);

builder.Services.AddOutputCache(o =>
    o.AddPolicy("breve", p => p.Expire(TimeSpan.FromSeconds(60))));

// ---------- accesso con Discord ----------
var accesso = ConfigAccesso.DaAmbiente();

// chiavi dei cookie di sessione: nel volume /keys, così un aggiornamento non disconnette nessuno
var cartellaChiavi = Environment.GetEnvironmentVariable("CARTELLA_CHIAVI") ?? "/keys";
var dp = builder.Services.AddDataProtection().SetApplicationName("fantaaoe");
if (Directory.Exists(cartellaChiavi)) dp.PersistKeysToFileSystem(new DirectoryInfo(cartellaChiavi));

var auth = builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "fantaaoe";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    });
if (accesso.Attivo)
{
    auth.AddDiscord(o =>
    {
        o.ClientId = accesso.ClientId!;
        o.ClientSecret = accesso.ClientSecret!;
        o.CallbackPath = "/api/auth/callback";
        o.Scope.Add("guilds");
        o.Scope.Add("guilds.members.read");
        o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Events.OnCreatingTicket = c => Accesso.CompletaTicket(c, accesso);
        o.Events.OnRemoteFailure = c =>
        {
            c.Response.Redirect("/?accesso=annullato");
            c.HandleResponse();
            return Task.CompletedTask;
        };
    });
}
builder.Services.AddAuthorization();
builder.Services.AddSingleton<AstaHub>();

var app = builder.Build();

// Dietro Funnel l'app vede http://jason-app:8080: per i cookie sicuri e per l'indirizzo di ritorno
// di Discord si usa l'indirizzo pubblico.
if (Uri.TryCreate(accesso.IndirizzoPubblico, UriKind.Absolute, out var pubblico))
{
    app.Use((ctx, next) =>
    {
        ctx.Request.Scheme = pubblico.Scheme;
        ctx.Request.Host = new HostString(pubblico.Authority);
        return next();
    });
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

// ---------- sessione ----------

app.MapGet("/api/auth/login", (string? ritorno) =>
    accesso.Attivo
        ? Results.Challenge(new AuthenticationProperties { RedirectUri = Accesso.RitornoSicuro(ritorno) }, ["Discord"])
        : Results.Redirect("/?accesso=non-configurato"));

app.MapGet("/api/auth/esci", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapGet("/api/io", async (ClaimsPrincipal u) =>
{
    var dentro = u.Identity?.IsAuthenticated == true;
    var admin = u.IsInRole(Accesso.Admin);
    if (dentro) await ApiTorneo.RegistraUtenteDiscord(db, u);
    return Results.Ok(new
    {
        loginAttivo = accesso.Attivo,
        autenticato = dentro,
        discordId = dentro ? u.FindFirst("discord_id")?.Value : null,
        nome = dentro ? u.FindFirst("nome")?.Value : null,
        avatar = dentro ? u.FindFirst("avatar")?.Value : null,
        membro = dentro && u.HasClaim(c => c.Type == "membro"),
        ruoli = dentro ? u.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() : Array.Empty<string>(),
        // solo per gli admin: serve a collegare i ruoli Discord ai pannelli
        configurazione = admin ? new
        {
            discordId = u.FindFirst("discord_id")?.Value,
            guildId = u.FindFirst("guild_id")?.Value,
            ruoliDiscord = u.FindAll("ruolo_discord").Select(c => c.Value).ToArray(),
        } : null,
    });
});

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

// ---------- numeri della community (landing) ----------

api.MapGet("/statistiche", async () =>
{
    if (db is null) return Results.Problem("Database non configurato", statusCode: 503);
    await using var c = await db.OpenConnectionAsync();
    var numeri = await c.QuerySingleAsync<Statistiche>("""
        select
          (select count(*) from giocatori) as giocatori_totali,
          (select count(*) from giocatori where ultimo_dito > now() - interval '90 days') as attivi90,
          (select count(*) from giocatori where ultimo_dito > now() - interval '30 days') as attivi30,
          (select count(*) from partite where valida) as partite_valide,
          (select count(*) from partite where valida and started_at > now() - interval '30 days') as partite30,
          (select count(*) from partite where valida and started_at > now() - interval '7 days') as partite7,
          (select coalesce(sum(durata_in_game_minuti), 0) from partite where valida) as minuti_totali,
          (select max(dito) from partite) as ultimo_dito,
          (select min(started_at) from partite) as prima_partita
        """);
    var settimane = await c.QueryAsync<Settimana>("""
        select date_trunc('week', s)::timestamp as inizio,
               (select count(*) from partite p
                 where p.valida and p.started_at >= date_trunc('week', s) and p.started_at < date_trunc('week', s) + interval '7 days') as partite
        from generate_series(now() - interval '15 weeks', now(), interval '1 week') s
        order by 1
        """);
    return Results.Ok(new { numeri, settimane });
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

// ---------- API interattive del torneo (Blocchi A, B, C) ----------
app.MappaApiTorneo(db, app.Services.GetRequiredService<AstaHub>());

// tutte le altre rotte non /api vanno alla SPA
app.MapFallbackToFile("index.html");

app.Run();
