using KarraMatcher.Domain.Applications;

namespace KarraMatcher.Application.Features.Applications;

/// <summary>Enda stället där en ansökan blir en DTO (`#194`).</summary>
internal static class ApplicationMapping
{
    public static ApplicationDto ToDto(this MembershipApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        return new ApplicationDto(
            application.Id,
            application.Account?.DisplayName,
            application.Account?.Email ?? string.Empty,
            application.Status.ToString(),
            new DateTimeOffset(application.CreatedUtc, TimeSpan.Zero));
    }
}
