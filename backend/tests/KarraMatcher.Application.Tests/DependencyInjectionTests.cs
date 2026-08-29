using FluentValidation;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Teams;
using KarraMatcher.Application.Features.Teams.GetTeamMatches;
using KarraMatcher.Application.Features.Teams.GetTeams;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ByggerEnGiltigServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddApplication();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddApplication_RegistrerarDispatchern()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IQueryDispatcher>());
    }

    [Fact]
    public void AddApplication_HittarHandlarnaAutomatiskt()
    {
        // Registreringen sker genom att scanna assemblyn. Gar den sonder registreras
        // ingenting, och felet skulle visa sig forst som ett 500 i drift.
        //
        // Attrappen for repositoryt maste med: handlarna registreras av Application, men
        // deras beroende implementeras i Infrastructure. Att ge containern attrappen
        // bevisar bade att handlern hittas och att den gar att konstruera.
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped<ITeamRepository>(_ => new FakeTeamRepository());
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider
            .GetService<IQueryHandler<GetTeamsQuery, IReadOnlyList<TeamDto>>>());
        Assert.NotNull(scope.ServiceProvider
            .GetService<IQueryHandler<GetTeamMatchesQuery, TeamMatchesDto?>>());
    }

    [Fact]
    public async Task AddApplication_HelaKedjanFungerarFranDispatcherTillHandler()
    {
        // Slutkontrollen: en fraga skickad genom den riktiga uppsattningen ska na fram.
        // Registrering, dispatcher, behaviors och handler i ett enda test.
        var repository = new FakeTeamRepository();
        repository.AddTeam("gul", "Gul");

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddScoped<ITeamRepository>(_ => repository);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        var result = await dispatcher.SendAsync(new GetTeamsQuery(), CancellationToken.None);

        Assert.Equal("gul", Assert.Single(result).Slug);
    }

    [Fact]
    public void AddApplication_HittarValidatorerna()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotEmpty(scope.ServiceProvider.GetServices<IValidator<GetTeamMatchesQuery>>());
    }

    [Fact]
    public void AddApplication_KopplarInValidationBehavior()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotEmpty(scope.ServiceProvider
            .GetServices<IQueryBehavior<GetTeamMatchesQuery, TeamMatchesDto?>>());
    }
}
