using Microsoft.AspNetCore.Mvc;
using static FantaAoe.Api.TorneoUtil;

namespace FantaAoe.Api.Controllers;

// Dashboard "Persone online" riservata agli admin: restituisce l'istantanea attuale di chi è connesso al sito.
// Gli aggiornamenti in diretta arrivano poi via SignalR (/hubs/asta, gruppo "admin-online").
[ApiController]
public class OnlineController : ControllerBase
{
    private readonly PresenzeSito sito;
    private readonly PresenzeAsta presenzeAsta;

    public OnlineController(PresenzeSito sito, PresenzeAsta presenzeAsta)
    {
        this.sito = sito;
        this.presenzeAsta = presenzeAsta;
    }

    [HttpGet("/api/admin/online")]
    public IResult Elenco()
    {
        if (!EAutenticato(User)) return Results.Unauthorized();
        if (!EAdmin(User)) return Results.Forbid();
        return Results.Ok(sito.Istantanea(presenzeAsta));
    }
}
