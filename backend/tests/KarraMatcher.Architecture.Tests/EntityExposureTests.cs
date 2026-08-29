using System.Reflection;
using System.Runtime.CompilerServices;

using KarraMatcher.Api.Diagnostics;
using KarraMatcher.Domain.Common;
using KarraMatcher.Domain.Matches;

namespace KarraMatcher.Architecture.Tests;

/// <summary>
/// Entiteter får aldrig lämna API:t. DTO:er är kontraktet utåt; en entitet i en
/// controllersignatur binder den publika ytan till databasmodellen och gör varje
/// schemaändring till en brytande API-ändring — CLAUDE.md → Backend, Use cases och data.
///
/// <para>
/// Räckvidd: regeln läser <em>controllersignaturer</em>, vilket är vad issue #10 begär
/// och där risken finns, eftersom regelverket föreskriver controllers plus MediatR.
/// Minimal-API-lambdor kan inte inspekteras med reflektion — deras typer finns bara i IL.
/// Börjar projektet bygga endpoints den vägen måste regeln kompletteras. Det är ett
/// medvetet val, inte ett förbiseende.
/// </para>
/// </summary>
public class EntityExposureTests
{
    private static readonly Assembly ApiAssembly = typeof(HealthChecks).Assembly;

    /// <summary>
    /// Domänentiteter = publika, icke-statiska klasser i domänlagret. Enum:ar är värden
    /// och statiska klasser är verktyg — varken det ena eller det andra är en entitet.
    /// </summary>
    internal static IReadOnlyCollection<Type> DomainEntities { get; } =
    [
        .. typeof(IDomainMarker).Assembly
            .GetTypes()
            .Where(t => t.IsClass && t.IsPublic && !(t.IsAbstract && t.IsSealed))
            .Where(t => !IsCompilerGenerated(t)),
    ];

    private static bool IsCompilerGenerated(Type type) =>
        type.Name.StartsWith('<')
        || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    /// <summary>
    /// Känner igen en MVC-controller på basklassens <em>namn</em> i stället för på typen.
    /// Det håller testprojektet fritt från ett ramverksberoende det annars inte behöver,
    /// och gör dessutom att självtesterna längst ned kan mata in en attrapp.
    /// </summary>
    internal static bool IsController(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Name is "ControllerBase" or "Controller")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Plockar isär en signaturtyp i de typer den faktiskt bär på.
    /// <c>Task&lt;ActionResult&lt;List&lt;Match&gt;&gt;&gt;</c> ska avslöja <c>Match</c> —
    /// annars räcker det med ett generiskt omslag för att gömma en entitet.
    /// </summary>
    internal static IEnumerable<Type> Unwrap(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.HasElementType)
        {
            var element = type.GetElementType();
            if (element is not null)
            {
                foreach (var inner in Unwrap(element))
                {
                    yield return inner;
                }
            }

            yield break;
        }

        yield return type;

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var inner in Unwrap(argument))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>
    /// Returnerar en läsbar rad per otillåten exponering. Tom lista betyder att regeln hålls.
    /// </summary>
    internal static IReadOnlyList<string> ForbiddenExposures(Type controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        var offenders = new List<string>();
        const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var method in controller.GetMethods(Declared))
        {
            if (method.IsSpecialName)
            {
                continue;
            }

            foreach (var entity in Unwrap(method.ReturnType).Where(DomainEntities.Contains))
            {
                offenders.Add($"{controller.Name}.{method.Name} returnerar {entity.Name}");
            }

            foreach (var parameter in method.GetParameters())
            {
                foreach (var entity in Unwrap(parameter.ParameterType).Where(DomainEntities.Contains))
                {
                    offenders.Add(
                        $"{controller.Name}.{method.Name} tar emot {entity.Name} som {parameter.Name}");
                }
            }
        }

        return offenders;
    }

    [Fact]
    public void Controllers_ExponerarIngaDomanentiteter()
    {
        var offenders = ApiAssembly
            .GetTypes()
            .Where(IsController)
            .SelectMany(ForbiddenExposures)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Entiteter exponeras direkt från API:t:{Environment.NewLine}"
                + string.Join(Environment.NewLine, offenders.Select(o => "  - " + o))
                + $"{Environment.NewLine}Använd en DTO (record) i stället — "
                + "CLAUDE.md → Backend, Use cases och data.");
    }

    [Fact]
    public void DomainEntities_HittarFaktiskaEntiteter()
    {
        // Utan den här kontrollen kan regeln ovan gå grön av fel skäl den dag typfiltret
        // slutar hitta något: noll kända entiteter kan aldrig exponeras.
        Assert.Contains(typeof(Match), DomainEntities);
        Assert.Contains(typeof(Venue), DomainEntities);
    }

    // ---- Självtester: bevisar att regeln faller när den bryts -------------------------
    //
    // Attrapperna nedan ligger i testassemblyn och scannas därför aldrig av regeln ovan,
    // som bara läser Api-assemblyn. De matas direkt in i detektorn.

    private abstract class ControllerBase;

    private sealed class ReturnerarEntitetController : ControllerBase
    {
        public Match Hamta(Guid id) => throw new NotSupportedException(nameof(id));
    }

    private sealed class GomdEntitetController : ControllerBase
    {
        public Task<IReadOnlyList<Venue>> Lista() => throw new NotSupportedException();
    }

    private sealed class TarEmotEntitetController : ControllerBase
    {
        public void Spara(Match match) => throw new NotSupportedException(nameof(match));
    }

    private sealed record MatchDto(Guid Id, string OpponentName);

    private sealed class ArtigController : ControllerBase
    {
        public Task<IReadOnlyList<MatchDto>> Lista() => throw new NotSupportedException();
    }

    [Fact]
    public void IsController_ArverControllerBase_GerTrue()
    {
        Assert.True(IsController(typeof(ArtigController)));
        Assert.False(IsController(typeof(MatchDto)));
    }

    [Fact]
    public void ForbiddenExposures_EntitetSomReturtyp_Faller()
    {
        var offenders = ForbiddenExposures(typeof(ReturnerarEntitetController));

        Assert.Contains(offenders, o => o.Contains("returnerar Match", StringComparison.Ordinal));
    }

    [Fact]
    public void ForbiddenExposures_EntitetGomdIGenerisktOmslag_Faller()
    {
        // Task<IReadOnlyList<Venue>> — två lager djupt. Utan Unwrap missas den.
        var offenders = ForbiddenExposures(typeof(GomdEntitetController));

        Assert.Contains(offenders, o => o.Contains("returnerar Venue", StringComparison.Ordinal));
    }

    [Fact]
    public void ForbiddenExposures_EntitetSomParameter_Faller()
    {
        var offenders = ForbiddenExposures(typeof(TarEmotEntitetController));

        Assert.Contains(offenders, o => o.Contains("tar emot Match", StringComparison.Ordinal));
    }

    [Fact]
    public void ForbiddenExposures_BaraDtoer_GerIngaTraffar()
    {
        Assert.Empty(ForbiddenExposures(typeof(ArtigController)));
    }
}
