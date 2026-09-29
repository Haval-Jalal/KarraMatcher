using FluentValidation;

using KarraMatcher.Application.Features.Events.Admin;
using KarraMatcher.Domain.Events;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Admin-vägens uppdatering av en trupp-händelse (`#332`, bug `#387`) måste lyda samma
/// draft-regler som coach-vägen. Tidigare fanns ingen validator för
/// <see cref="UpdateTruppEventCommand"/>, så en admin kunde spara en händelse som coach-vägen
/// alltid avvisar — t.ex. en lokal-tid som bryter §KM.5.
/// </summary>
public class UpdateTruppEventCommandValidatorTests
{
    private readonly UpdateTruppEventCommandValidator _validator = new();

    private static EventDraft ValidMatch() => new(
        EventType.Match,
        new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc),
        Title: null,
        Opponent: "Torslanda",
        IsHome: true,
        Address: null,
        Note: null);

    [Fact]
    public void GiltigtDraft_ArGodkant()
    {
        var command = new UpdateTruppEventCommand(
            Guid.NewGuid(), Guid.NewGuid(), ValidMatch(), Guid.NewGuid());

        Assert.True(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void LokalStarttid_ArOgiltig()
    {
        // §KM.5: en lokal tid sparad rakt av blir två timmar fel på sommaren.
        var draft = ValidMatch() with
        {
            KickoffUtc = new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Local),
        };
        var command = new UpdateTruppEventCommand(
            Guid.NewGuid(), Guid.NewGuid(), draft, Guid.NewGuid());

        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void MatchUtanMotstandare_ArOgiltig()
    {
        var draft = ValidMatch() with { Opponent = "" };
        var command = new UpdateTruppEventCommand(
            Guid.NewGuid(), Guid.NewGuid(), draft, Guid.NewGuid());

        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void TomtEventId_ArOgiltigt()
    {
        var command = new UpdateTruppEventCommand(
            Guid.NewGuid(), Guid.Empty, ValidMatch(), Guid.NewGuid());

        Assert.False(_validator.Validate(command).IsValid);
    }

    [Fact]
    public void DispatchernHittarValidatorn()
    {
        // Utan registrering hoppar CommandValidationBehavior tyst över valideringen — hela
        // poängen med #387 är att den nu faktiskt körs.
        var services = new ServiceCollection();
        services.AddApplication();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotEmpty(scope.ServiceProvider.GetServices<IValidator<UpdateTruppEventCommand>>());
    }
}
