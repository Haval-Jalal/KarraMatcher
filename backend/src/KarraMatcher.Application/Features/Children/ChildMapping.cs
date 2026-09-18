using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Features.Children;

/// <summary>
/// Enda stället där barn och vårdnadshavare blir DTO:er (§KM.1, `#196`). Här bildas "Liam J"
/// — hela efternamnet finns aldrig att exponera.
/// </summary>
internal static class ChildMapping
{
    public static string DisplayName(string firstName, string lastInitial) =>
        $"{firstName} {lastInitial}".Trim();

    public static ChildDto ToDto(this Child child, IReadOnlyList<GuardianRefDto> guardians)
    {
        ArgumentNullException.ThrowIfNull(child);

        return new ChildDto(
            child.Id,
            child.FirstName,
            child.LastInitial,
            DisplayName(child.FirstName, child.LastInitial),
            child.TeamId,
            child.Team?.Name,
            guardians);
    }

    public static GuardianRefDto ToRef(this Guardianship guardianship)
    {
        ArgumentNullException.ThrowIfNull(guardianship);

        return new GuardianRefDto(
            guardianship.AccountId,
            guardianship.Account?.DisplayName,
            guardianship.Account?.Email ?? string.Empty);
    }

    public static RosterTeamDto ToRosterTeam(this Team team)
    {
        ArgumentNullException.ThrowIfNull(team);

        return new RosterTeamDto(team.Id, team.Name, team.ColorHex);
    }
}
