using KarraMatcher.Application.Features.Auth;

using Microsoft.AspNetCore.Authorization;

namespace KarraMatcher.Api.Features.Auth;

/// <summary>Kravet att vara admin för just den trupp adressen gäller (`#193`).</summary>
public sealed class AdminOfTruppRequirement : IAuthorizationRequirement
{
    /// <summary>Routevärdet som bär truppens id (AgeGroup-guid, som anspråket).</summary>
    public const string RouteValue = "truppId";
}

/// <summary>
/// Avgör om den inloggade är admin för truppen i adressen.
///
/// <para>
/// Superadmin kortsluts — global ägare når alla trupper. För övriga jämförs
/// <c>admin-trupp</c>-anspråket (som bär truppens id) mot id:t i routen, utan databasfråga.
/// Saknas id:t i adressen nekas anropet: ett krav som inte hittar sin trupp ska falla
/// synligt, inte se ut att skydda.
/// </para>
/// </summary>
internal sealed class AdminOfTruppHandler(IHttpContextAccessor accessor)
    : AuthorizationHandler<AdminOfTruppRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminOfTruppRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (Membership.IsSuperAdmin(context.User))
        {
            context.Succeed(requirement);

            return Task.CompletedTask;
        }

        var truppId = accessor.HttpContext?.Request.RouteValues[AdminOfTruppRequirement.RouteValue]
            as string;

        if (!string.IsNullOrWhiteSpace(truppId)
            && context.User.HasClaim(AuthClaims.AdminOfTrupp, truppId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
