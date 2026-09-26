using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Home;

/// <summary>
/// Hem-vyns sammanställning för ett inloggat konto. Kontot kommer ur token, aldrig ur kroppen,
/// så en medlem kan bara hämta sin egen översikt (§KM.3).
/// </summary>
public sealed record GetHomeSummaryQuery(Guid AccountId) : IQuery<HomeSummaryDto>;
