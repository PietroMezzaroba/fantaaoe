using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace FantaAoe.Api;

/// <summary>Il controller esiste solo con ASPNETCORE_ENVIRONMENT=Development.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SoloSviluppoAttribute : Attribute;

/// <summary>Toglie dalle rotte i controller [SoloSviluppo]: fuori da Development rispondono 404 come se non ci fossero.</summary>
public sealed class EscludiSoloSviluppo : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var t in feature.Controllers.Where(t => t.GetCustomAttribute<SoloSviluppoAttribute>() is not null).ToList())
            feature.Controllers.Remove(t);
    }
}
