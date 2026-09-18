using System.Security.Claims;

using KarraMatcher.Application.Features.Auth;

using Microsoft.AspNetCore.Authorization;

namespace KarraMatcher.Api.Features.Auth;

/// <summary>
/// Appens behörighetsregler, samlade på ett ställe.
///
/// <para>
/// Regelverket säger att rollkontroller aldrig hårdkodas i controllers (§KM.3). Skälet är
/// inte prydlighet: en kontroll som står i en controller går att glömma i nästa, och det
/// finns ingen plats att läsa för att se vad som faktiskt gäller. Här går hela
/// behörighetsmodellen att läsa på en skärm.
/// </para>
///
/// <para>
/// Ingen av policyerna gäller något som helst i dag — tränarens endpoints byggs i M3.
/// Att de finns först är avsiktligt: det är enklare att bygga en endpoint mot en färdig
/// regel än att lägga på behörighet efteråt, när det redan finns kod som fungerar utan.
/// </para>
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Superadmin — global ägare av plattformen (v2). Den enda rollen som får skapa och
    /// ändra sporter, klubbar, trupper och lag och tillsätta admins (§KM.3, `#192`).
    ///
    /// <para>
    /// Egen policy och inte <see cref="Admin"/>: i dag råkar bara superadmin bära
    /// <c>role=admin</c> (bakåtkompat), men så fort riktiga trupp-admins tillsätts slutar
    /// <see cref="Admin"/> vara superadmin-exklusiv. Superadmin-ytan måste låsas mot
    /// anspråket <c>superadmin</c> självt, inte mot en roll som snart delas.
    /// </para>
    /// </summary>
    public const string SuperAdmin = "superadmin";

    /// <summary>Administratör. Får agera på alla lag.</summary>
    public const string Admin = "admin";

    /// <summary>Tränare för minst ett lag, utan att säga vilket.</summary>
    public const string AnyCoach = "tranare";

    /// <summary>
    /// Tränare för <em>det lag anropet gäller</em>, eller administratör.
    ///
    /// <para>
    /// Den här är den som betyder något. En tränare för Gul som anropar Blås endpoint har
    /// en giltig token, en giltig roll och ett giltigt anrop — och ska ändå nekas. Det är
    /// objektnivå-auktorisering (checklistan 2.6), och det är den kontroll som glöms bort
    /// oftast eftersom allt ser rätt ut utan den.
    /// </para>
    /// </summary>
    public const string CoachOfTeam = "tranare-for-laget";

    /// <summary>
    /// Admin för <em>den trupp adressen gäller</em>, eller superadmin (v2, `#193`).
    ///
    /// <para>
    /// Som <see cref="CoachOfTeam"/> fast för en trupp: anspråket <c>admin-trupp</c> bär
    /// truppens id, och routen måste bära samma id — en admin för en trupp når inte en
    /// annans genom att byta id i kroppen, för det finns inget id i kroppen att byta.
    /// </para>
    /// </summary>
    public const string AdminOfTrupp = "admin-for-truppen";

    /// <summary>Medlem av laget i adressen — admin, tränare eller vårdnadshavare (v2, §KM.3).</summary>
    public const string MemberOfTeam = "medlem-av-laget";

    /// <summary>Medlem av händelsens lag (v2, §KM.3, `#198`).</summary>
    public const string MemberOfEvent = "medlem-av-handelsen";

    public static AuthorizationOptions AddKarraPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(SuperAdmin, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(AuthClaims.SuperAdmin, "true"));

        options.AddPolicy(Admin, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(AuthClaims.AdminRole));

        options.AddPolicy(AnyCoach, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context => IsAdmin(context.User) || IsCoachOfSomething(context.User)));

        options.AddPolicy(CoachOfTeam, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new CoachOfTeamRequirement()));

        options.AddPolicy(AdminOfTrupp, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new AdminOfTruppRequirement()));

        options.AddPolicy(MemberOfTeam, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new MemberOfTeamRequirement()));

        options.AddPolicy(MemberOfEvent, policy => policy
            .RequireAuthenticatedUser()
            .AddRequirements(new MemberOfEventRequirement()));

        return options;
    }

    internal static bool IsAdmin(ClaimsPrincipal user) =>
        user.IsInRole(AuthClaims.AdminRole);

    internal static bool IsCoachOfSomething(ClaimsPrincipal user) =>
        user.HasClaim(claim => claim.Type == AuthClaims.Coach);

    internal static bool IsCoachOf(ClaimsPrincipal user, string teamSlug) =>
        user.HasClaim(AuthClaims.Coach, teamSlug);
}
