using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// Enda stället där plattformsentiteterna blir DTO:er (§KM.3, `#192`). Samlat, av samma
/// skäl som <c>TeamMapping</c>: det ska vara svårt att råka exponera ett fält som inte hör
/// hemma i ett svar.
/// </summary>
internal static class AdministrationMapping
{
    public static SportDto ToDto(this Sport sport) => new(sport.Id, sport.Name, sport.Slug);

    public static ClubDto ToDto(this Club club) => new(club.Id, club.Name, club.Slug);

    public static TruppDto ToDto(this AgeGroup trupp) => new(
        trupp.Id,
        trupp.ClubId,
        trupp.Club?.Name ?? string.Empty,
        trupp.SportId,
        trupp.Sport?.Name ?? string.Empty,
        trupp.Name,
        trupp.Season);

    public static LagDto ToLagDto(this Team lag) =>
        new(lag.Id, lag.AgeGroupId, lag.Name, lag.ColorHex, lag.Slug);

    /// <summary>En admin-rad byggd av rollen och dess konto. Rollen bär tidsstämpeln.</summary>
    public static TruppAdminDto ToAdminDto(this TeamRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        var account = role.Account;

        return new TruppAdminDto(
            role.AccountId,
            account?.DisplayName,
            account?.Email ?? string.Empty,
            new DateTimeOffset(role.GrantedUtc, TimeSpan.Zero));
    }

    /// <summary>En tränar-rad byggd av rollen och dess konto (`#197`).</summary>
    public static TeamCoachDto ToCoachDto(this TeamRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        var account = role.Account;

        return new TeamCoachDto(
            role.AccountId,
            account?.DisplayName,
            account?.Email ?? string.Empty,
            new DateTimeOffset(role.GrantedUtc, TimeSpan.Zero));
    }
}
