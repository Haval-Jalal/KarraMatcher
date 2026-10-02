using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// `CoachOfTeam`-handlern (#580, §KM.3). Superadmin-kortslutningen ska bero på det
/// superadmin-specifika anspråket, inte på rollen `admin`. Rollen utfärdas i dag bara till
/// superadmin, men om en riktig trupp-admin någon gång får `role=admin` får det INTE tyst göra
/// tränar-behörigheten global över alla trupper/klubbar.
/// </summary>
public sealed class CoachOfTeamHandlerTests
{
    private static readonly Guid TeamTruppId = Guid.NewGuid();

    /// <summary>Stub: bara lagets trupp-id behövs; övriga medlemskaps-frågor rörs inte av handlern.</summary>
    private sealed class StubMembership : IMembershipService
    {
        public Task<Guid?> TruppIdForTeamBySlugAsync(string slug, CancellationToken ct) =>
            Task.FromResult<Guid?>(TeamTruppId);

        public Task<bool> IsMemberOfTeamAsync(Guid a, Guid t, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<MemberTruppDto>> MemberTrupperAsync(Guid a, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> IsLeaderOfTruppAsync(Guid a, Guid g, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> IsMemberOfTruppAsync(Guid a, Guid g, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Guid>> MemberAccountIdsForTruppAsync(Guid g, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Guid>> MemberAccountIdsAsync(Guid t, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> IsMemberOfTeamBySlugAsync(Guid a, string s, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> IsMemberOfEventAsync(Guid a, Guid e, CancellationToken ct) => throw new NotImplementedException();
        public Task<MatchVisibility> GetMatchVisibilityAsync(Guid a, CancellationToken ct) => throw new NotImplementedException();
        public Task<string?> TruppNameAsync(Guid g, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<TeamChannelInfo>> AccessibleTeamChannelsAsync(Guid a, Guid g, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<string>> MemberTeamSlugsAsync(Guid a, CancellationToken ct) => throw new NotImplementedException();
    }

    private static async Task<bool> SucceedsAsync(ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues[CoachOfTeamRequirement.RouteValue] = "gul";

        var handler = new CoachOfTeamHandler(
            new HttpContextAccessor { HttpContext = httpContext }, new StubMembership());
        var requirement = new CoachOfTeamRequirement();
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(context);

        return context.HasSucceeded;
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    [Fact]
    public async Task RoleAdminUtanSuperadminAnsprak_Nekas()
    {
        // role=admin men inget superadmin-anspråk: IsInRole("admin") är sant (gamla koden hade
        // släppt igenom), men den nya kortslutningen kräver det superadmin-specifika anspråket.
        var user = Principal(new Claim(ClaimTypes.Role, AuthClaims.AdminRole));

        Assert.False(await SucceedsAsync(user));
    }

    [Fact]
    public async Task SuperadminAnsprak_SlapperIgenom()
    {
        var user = Principal(new Claim(AuthClaims.SuperAdmin, "true"));

        Assert.True(await SucceedsAsync(user));
    }

    [Fact]
    public async Task TruppAdminForLagetsTrupp_SlapperIgenom()
    {
        // Den legitima trupp-admin-vägen (admin-trupp-anspråket) ska fortsatt fungera.
        var user = Principal(new Claim(AuthClaims.AdminOfTrupp, TeamTruppId.ToString()));

        Assert.True(await SucceedsAsync(user));
    }
}
