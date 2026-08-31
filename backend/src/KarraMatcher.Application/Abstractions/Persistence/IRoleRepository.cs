using KarraMatcher.Application.Features.Auth;

namespace KarraMatcher.Application.Abstractions.Persistence;

public interface IRoleRepository
{
    /// <summary>
    /// Kontots roller, med lagen som slug.
    ///
    /// <para>
    /// Anropas när en session utfärdas eller förnyas, alltså högst var femtonde minut
    /// per inloggad. Rollerna hamnar sedan i token, så varje efterföljande anrop kan
    /// avgöras utan att databasen frågas.
    /// </para>
    /// </summary>
    public Task<AccountRoles> GetRolesAsync(Guid accountId, CancellationToken cancellationToken);
}
