using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Calendar;

/// <summary>Kontots kalender-URL (`#307`-uppföljning). Skapar en nyckel vid första anropet.</summary>
public sealed record GetCalendarLinkQuery(Guid AccountId) : IQuery<CalendarLinkDto>;

/// <summary>Byter ut kontots kalender-nyckel — den gamla länken slutar fungera direkt.</summary>
public sealed record RegenerateCalendarTokenCommand(Guid AccountId) : ICommand<CalendarLinkDto>;

/// <summary>Feeden för en nyckel. Null om nyckeln är okänd/återkallad (controllern gör 404).</summary>
public sealed record GetCalendarFeedQuery(string Token) : IQuery<string?>;

/// <summary>Kontots kalender-länk att prenumerera på.</summary>
public sealed record CalendarLinkDto(string Url);

internal sealed class GetCalendarLinkQueryHandler(CalendarService service)
    : IQueryHandler<GetCalendarLinkQuery, CalendarLinkDto>
{
    public async Task<CalendarLinkDto> HandleAsync(
        GetCalendarLinkQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var url = await service.GetLinkAsync(query.AccountId, cancellationToken).ConfigureAwait(false);

        return new CalendarLinkDto(url);
    }
}

internal sealed class RegenerateCalendarTokenCommandHandler(CalendarService service)
    : ICommandHandler<RegenerateCalendarTokenCommand, CalendarLinkDto>
{
    public async Task<CalendarLinkDto> HandleAsync(
        RegenerateCalendarTokenCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var url = await service.RegenerateAsync(command.AccountId, cancellationToken)
            .ConfigureAwait(false);

        return new CalendarLinkDto(url);
    }
}

internal sealed class GetCalendarFeedQueryHandler(CalendarService service)
    : IQueryHandler<GetCalendarFeedQuery, string?>
{
    public async Task<string?> HandleAsync(
        GetCalendarFeedQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await service.BuildFeedAsync(query.Token, cancellationToken).ConfigureAwait(false);
    }
}
