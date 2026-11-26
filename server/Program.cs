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

namespace FantaAoe.Api;

public class Program
{
    public static void Main(string[] args)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var builder = WebApplication.CreateBuilder(args);

        var connessione = builder.Configuration.GetConnectionString("Aoe");
        NpgsqlDataSource? db = string.IsNullOrWhiteSpace(connessione) ? null : NpgsqlDataSource.Create(connessione);

        builder.Services.AddControllers(o =>
                // i body dei client non hanno mai richiesto i campi non annullabili: niente 400 impliciti
                o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .ConfigureApplicationPartManager(pm =>
            {
                // i controller marcati [SoloSviluppo] (login simulato) non esistono fuori da Development
                if (!builder.Environment.IsDevelopment()) pm.FeatureProviders.Add(new EscludiSoloSviluppo());
            });
        if (db is not null) builder.Services.AddSingleton(db);

        // Swagger solo in sviluppo: in produzione non viene registrato né esposto
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
        }

        builder.Services.AddOutputCache(o =>
            o.AddPolicy("breve", p => p.Expire(TimeSpan.FromSeconds(60))));

        // ---------- accesso con Discord ----------
        var accesso = ConfigAccesso.DaAmbiente(builder.Configuration);
        builder.Services.AddSingleton(accesso);

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
        // un collegamento caduto di colpo (browser chiuso, rete spenta) viene rilevato in pochi secondi: ping ogni 5 s, scaduto dopo 15 s
        builder.Services.AddSignalR(o =>
        {
            o.KeepAliveInterval = TimeSpan.FromSeconds(5);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(15);
        });
        builder.Services.AddSingleton<PresenzeAsta>();
        builder.Services.AddSingleton<TimerAsta>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<TimerAsta>());

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

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseDefaultFiles();
        var tipiFile = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
        tipiFile.Mappings[".glb"] = "model/gltf-binary";
        tipiFile.Mappings[".gltf"] = "model/gltf+json";
        app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = tipiFile });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseOutputCache();

        app.MapControllers();
        app.MapHub<AstaHub>(AstaHub.Percorso);

        // una rotta /api che non esiste è un 404 in JSON (non la pagina della SPA, che il client scambierebbe per dati)
        app.MapFallback("/api/{**percorso}", (HttpContext c) =>
            Results.Json(new { errore = $"Rotta API non trovata: {c.Request.Method} {c.Request.Path}" }, statusCode: 404));

        // tutte le altre rotte non /api vanno alla SPA
        app.MapFallbackToFile("index.html");

        app.Run();
    }
}
