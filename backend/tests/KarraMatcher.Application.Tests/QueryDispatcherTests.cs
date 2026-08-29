using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Dispatchern ersätter MediatR (se ADR i <c>docs/PROJEKT-HANDOFF.md</c>). Den är egen kod,
/// och egen kod i en så central position måste vara bevisad — inte antagen.
/// </summary>
public class QueryDispatcherTests
{
    private sealed record EkoQuery(string Text) : IQuery<string>;

    private sealed class EkoHandler : IQueryHandler<EkoQuery, string>
    {
        public Task<string> HandleAsync(EkoQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(query.Text);
    }

    private sealed class EkoValidator : AbstractValidator<EkoQuery>
    {
        public EkoValidator() => RuleFor(q => q.Text).NotEmpty().WithMessage("Text kravs.");
    }

    /// <summary>Skriver sitt märke i listan både före och efter handlern.</summary>
    private sealed class SparandeBehavior(List<string> spar, string mark)
        : IQueryBehavior<EkoQuery, string>
    {
        public async Task<string> HandleAsync(
            EkoQuery query,
            Func<Task<string>> continuation,
            CancellationToken cancellationToken)
        {
            spar.Add($"{mark}-fore");
            var result = await continuation();
            spar.Add($"{mark}-efter");
            return result;
        }
    }

    /// <summary>
    /// Ger ett scope och inte rotcontainern. Allt här är <c>Scoped</c>, precis som i
    /// appen, och <c>validateScopes</c> vägrar med rätta lämna ut en scoped tjänst från
    /// roten — det är samma kontroll som skulle fånga en läcka i produktionskoden.
    /// </summary>
    private static (ServiceProvider Provider, IServiceScope Scope) BuildScope(
        Action<IServiceCollection>? extra = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<IQueryHandler<EkoQuery, string>, EkoHandler>();
        extra?.Invoke(services);

        var provider = services.BuildServiceProvider(validateScopes: true);
        return (provider, provider.CreateScope());
    }

    [Fact]
    public async Task SendAsync_HittarHandlern()
    {
        var (provider, scope) = BuildScope();
        using var _ = provider;
        using var __ = scope;
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.SendAsync(new EkoQuery("hej"), CancellationToken.None);

        Assert.Equal("hej", result);
    }

    [Fact]
    public async Task SendAsync_UtanRegistreradHandler_Kastar()
    {
        // Ett glömt handler-registrering ska smälla högt och tidigt, inte returnera null.
        var services = new ServiceCollection();
        services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new EkoQuery("hej"), CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_Null_Kastar()
    {
        var (provider, scope) = BuildScope();
        using var _ = provider;
        using var __ = scope;
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => dispatcher.SendAsync<string>(null!, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_FleraBehaviors_KorsIRegistreringsordning()
    {
        // Ordningen är inte en detalj: valideringen ligger först och ska hinna avbryta
        // innan något annat steg gör något.
        var spar = new List<string>();
        var (provider, scope) = BuildScope(services =>
        {
            services.AddScoped<IQueryBehavior<EkoQuery, string>>(_ => new SparandeBehavior(spar, "yttre"));
            services.AddScoped<IQueryBehavior<EkoQuery, string>>(_ => new SparandeBehavior(spar, "inre"));
        });
        using var _ = provider;
        using var __ = scope;
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        await dispatcher.SendAsync(new EkoQuery("hej"), CancellationToken.None);

        Assert.Equal(
            ["yttre-fore", "inre-fore", "inre-efter", "yttre-efter"],
            spar);
    }

    [Fact]
    public async Task SendAsync_OgiltigFraga_AvbrytsInnanHandlern()
    {
        // Beviset är att steget efter valideringen aldrig hinner skriva något: kedjan
        // bryts där och når varken nästa behavior eller handlern.
        var spar = new List<string>();
        var (provider, scope) = BuildScope(services =>
        {
            services.AddScoped<IValidator<EkoQuery>, EkoValidator>();
            services.AddScoped<IQueryBehavior<EkoQuery, string>>(sp =>
                new ValidationBehavior<EkoQuery, string>(sp.GetServices<IValidator<EkoQuery>>()));
            services.AddScoped<IQueryBehavior<EkoQuery, string>>(_ =>
                new SparandeBehavior(spar, "efter-validering"));
        });
        using var _ = provider;
        using var __ = scope;
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.SendAsync(new EkoQuery(string.Empty), CancellationToken.None));

        Assert.Empty(spar);
        Assert.Contains(exception.Errors, e => e.ErrorMessage == "Text kravs.");
    }

    [Fact]
    public async Task SendAsync_GiltigFraga_PasserarValideringen()
    {
        var (provider, scope) = BuildScope(services =>
        {
            services.AddScoped<IValidator<EkoQuery>, EkoValidator>();
            services.AddScoped<IQueryBehavior<EkoQuery, string>>(sp =>
                new ValidationBehavior<EkoQuery, string>(sp.GetServices<IValidator<EkoQuery>>()));
        });
        using var _ = provider;
        using var __ = scope;
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.SendAsync(new EkoQuery("hej"), CancellationToken.None);

        Assert.Equal("hej", result);
    }

    [Fact]
    public async Task SendAsync_SammaFragaTvaGanger_AteranvanderOmslaget()
    {
        // Omslaget cachas per frågetyp. Cachen är statisk och delas mellan instanser, så
        // den måste tåla att användas om — annars hade första anropet fungerat och det
        // andra kastat.
        var (provider, scope) = BuildScope();
        using var _ = provider;
        using var __ = scope;
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var first = await dispatcher.SendAsync(new EkoQuery("ett"), CancellationToken.None);
        var second = await dispatcher.SendAsync(new EkoQuery("tva"), CancellationToken.None);

        Assert.Equal("ett", first);
        Assert.Equal("tva", second);
    }
}
