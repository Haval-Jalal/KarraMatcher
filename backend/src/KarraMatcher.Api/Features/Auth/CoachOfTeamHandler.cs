using Microsoft.AspNetCore.Authorization;

namespace KarraMatcher.Api.Features.Auth;

/// <summary>Kravet att vara tränare för just det lag anropet gäller.</summary>
public sealed class CoachOfTeamRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Routevärdet som bär laget. Slug och inte id, eftersom appens adresser är byggda
    /// på slug och anspråken därför kan jämföras utan en databasfråga.
    /// </summary>
    public const string RouteValue = "slug";
}

/// <summary>
/// Avgör om den inloggade är tränare för laget i adressen.
///
/// <para>
/// <b>Saknas laget i adressen nekas anropet.</b> Det är den viktigaste raden i hela
/// klassen. Ett krav som inte hittar sitt lag och därför släpper igenom vore värre än
/// inget krav alls — det skulle se ut att skydda. En endpoint som råkar sakna
/// <c>{slug}</c> ska falla direkt och synligt.
/// </para>
/// </summary>
internal sealed class CoachOfTeamHandler(IHttpContextAccessor accessor)
    : AuthorizationHandler<CoachOfTeamRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CoachOfTeamRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Admin först: rollen gäller alla lag, och behöver inget lag i adressen.
        if (AuthorizationPolicies.IsAdmin(context.User))
        {
            context.Succeed(requirement);

            return Task.CompletedTask;
        }

        var slug = accessor.HttpContext?.Request.RouteValues[CoachOfTeamRequirement.RouteValue]
            as string;

        if (!string.IsNullOrWhiteSpace(slug)
            && AuthorizationPolicies.IsCoachOf(context.User, slug))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
