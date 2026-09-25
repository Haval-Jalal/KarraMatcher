using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Auth;

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
/// Avgör om den inloggade får sköta laget i adressen — dvs. är tränare för dess trupp.
///
/// <para>
/// En tränare gäller <em>hela</em> truppen (`#287`), så en trupp-tränare får sköta vilket som
/// helst av truppens färg-lags scheman. Superadmin och det första anspråket (coach-slug ur
/// token) släpps igenom direkt; annars slås ledarskapet för lagets trupp upp mot databasen.
/// </para>
///
/// <para>
/// <b>Saknas laget i adressen nekas anropet.</b> Det är den viktigaste raden i hela klassen.
/// Ett krav som inte hittar sitt lag och därför släpper igenom vore värre än inget krav alls —
/// det skulle se ut att skydda. En endpoint som råkar sakna <c>{slug}</c> ska falla synligt.
/// </para>
/// </summary>
internal sealed class CoachOfTeamHandler(
    IHttpContextAccessor accessor,
    IMembershipService membership)
    : AuthorizationHandler<CoachOfTeamRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CoachOfTeamRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Superadmin först: gäller allt, och behöver ingen databasfråga.
        if (AuthorizationPolicies.IsAdmin(context.User))
        {
            context.Succeed(requirement);
            return;
        }

        var slug = accessor.HttpContext?.Request.RouteValues[CoachOfTeamRequirement.RouteValue]
            as string;

        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        // Per-lag-tränare via lag-anspråket i token (ingen databasfråga) — den snabba vägen.
        if (AuthorizationPolicies.IsCoachOf(context.User, slug))
        {
            context.Succeed(requirement);
            return;
        }

        // Annars: en trupp-tränare får sköta alla sina färg-lag. Vi slår upp lagets trupp och
        // jämför mot admin-trupp-anspråket — samma anspråk som AdminOfTrupp använder (`#287`).
        var ct = accessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var truppId = await membership.TruppIdForTeamBySlugAsync(slug, ct).ConfigureAwait(false);

        if (truppId is not null
            && context.User.HasClaim(AuthClaims.AdminOfTrupp, truppId.Value.ToString()))
        {
            context.Succeed(requirement);
        }
    }
}
