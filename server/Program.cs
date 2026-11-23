// Scheletro di partenza: Jason può sostituirlo con il suo progetto.
// Regole del server: HTTP sulla 8080 (l'HTTPS è davanti), niente file su disco,
// configurazione e segreti da variabili d'ambiente.

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// la SPA Vue compilata sta in wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// endpoint di prova
// "versione" = commit pubblicato (lo passa GitHub Actions)
app.MapGet("/api/salute", () => Results.Ok(new
{
    stato = "ok",
    versione = Environment.GetEnvironmentVariable("VERSIONE") ?? "locale",
    ora = DateTime.UtcNow,
}));

// tutte le altre rotte non /api vanno alla SPA
app.MapFallbackToFile("index.html");

app.Run();
