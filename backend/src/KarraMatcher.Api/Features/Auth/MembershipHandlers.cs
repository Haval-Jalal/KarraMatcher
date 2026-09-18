using System.Security.Claims;

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Auth;

/// <summary>Kravet att vara medlem av laget i adressen (v2, `#191`).</summary>
public sealed class MemberOfTeamRequirement : IAuthorizationRequirement
{
    /// <summary>Routevärdet som bär lagets slug.</summary>
    public const string RouteValue = "slug";
}

/// <summary>Kravet att vara medlem av händelsens lag (v2, `#191`, `#198`).</summary>
public sealed class MemberOfEventRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Routevärden som kan bära händelsens id, i den ordning de prövas. <c>matchId</c> är kvar
    /// eftersom närvaro och samåkning behåller det namnet på sin väg (§KM.12, `#198`).
    /// </summary>
    public static readonly string[] RouteValues = ["eventId", "matchId", "id"];
}

/// <summary>
/// Delad grund för medlemskaps-handlers (§KM.3, v2).
///
/// <para>
/// <b>Superadmin kortsluts via sitt anspråk</b> — global ägare ser allt, utan en
/// databasfråga. För alla andra avgör <see cref="IMembershipService"/> mot databasen: är
/// kontot admin för truppen, tränare för laget, eller vårdnadshavare till ett barn i laget?
/// Saknas underlaget i adressen nekas anropet — ett krav som inte hittar sin resurs ska
/// falla synligt, inte se ut att skydda.
/// </para>
/// </summary>
internal static class Membership
{
    public static bool IsSuperAdmin(ClaimsPrincipal user) =>
        user.HasClaim(AuthClaims.SuperAdmin, "true");

    public static Guid? AccountId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

internal sealed class MemberOfTeamHandler(
    IHttpContextAccessor accessor,
    IMembershipService membership)
    : AuthorizationHandler<MemberOfTeamRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MemberOfTeamRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Membership.IsSuperAdmin(context.User))
        {
            context.Succeed(requirement);
            return;
        }

        var accountId = Membership.AccountId(context.User);
        var slug = accessor.HttpContext?.Request.RouteValues[MemberOfTeamRequirement.RouteValue]
            as string;

        if (accountId is null || string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        var ct = accessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        if (await membership.IsMemberOfTeamBySlugAsync(accountId.Value, slug, ct)
            .ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}

internal sealed class MemberOfEventHandler(
    IHttpContextAccessor accessor,
    IMembershipService membership)
    : AuthorizationHandler<MemberOfEventRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MemberOfEventRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Membership.IsSuperAdmin(context.User))
        {
            context.Succeed(requirement);
            return;
        }

        var accountId = Membership.AccountId(context.User);
        var routeValues = accessor.HttpContext?.Request.RouteValues;

        var eventId = MemberOfEventRequirement.RouteValues
            .Select(key => routeValues?[key] as string)
            .FirstOrDefault(value => Guid.TryParse(value, out _));

        if (accountId is null || !Guid.TryParse(eventId, out var id))
        {
            return;
        }

        var ct = accessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        if (await membership.IsMemberOfEventAsync(accountId.Value, id, ct).ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}
