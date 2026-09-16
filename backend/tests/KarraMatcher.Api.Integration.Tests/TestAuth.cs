using System.Net.Http.Headers;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Autentiseringshjälpare för integrationstester (v2, `#191`).
///
/// <para>
/// Appen är stängd: nästan varje läsning kräver nu en inloggad medlem (§KM.3). En superadmin
/// ser allt, så en superadmin-klient är den enklaste nyckeln för ett test som bara vill nå
/// innehållet utan att först bygga upp ett medlemskap. Rollen seedas som en riktig rad —
/// medlemskapstjänsten är auktoritativ mot databasen, inte mot token.
/// </para>
/// </summary>
public static class TestAuth
{
    private const string SuperAdminEmail = "superadmin@example.test";

    /// <summary>En klient inloggad som superadmin — ser alla lag och matcher.</summary>
    public static HttpClient CreateSuperAdminClient(this WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var accountId = EnsureSuperAdmin(factory.Services);
        var token = TokenFor(
            factory.Services, accountId, new AccountRoles(true, [], []), SuperAdminEmail);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Utfärdar en access-token för ett konto med angivna roller.</summary>
    public static string TokenFor(
        IServiceProvider services, Guid accountId, AccountRoles roles, string email)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        return issuer.Issue(accountId, email, roles).Token;
    }

    /// <summary>Skapar (idempotent) ett superadmin-konto med en global superadmin-roll.</summary>
    private static Guid EnsureSuperAdmin(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var account = context.Accounts.FirstOrDefault(a => a.Email == SuperAdminEmail);

        if (account is null)
        {
            account = new Account
            {
                Id = Guid.NewGuid(),
                Email = SuperAdminEmail,
                CreatedUtc = DateTime.UtcNow,
            };

            context.Accounts.Add(account);
            context.SaveChanges();
        }

        var hasRole = context.TeamRoles
            .Any(r => r.AccountId == account.Id && r.Role == RoleKind.SuperAdmin);

        if (!hasRole)
        {
            context.TeamRoles.Add(new TeamRole
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Role = RoleKind.SuperAdmin,
                GrantedUtc = DateTime.UtcNow,
            });

            context.SaveChanges();
        }

        return account.Id;
    }
}
