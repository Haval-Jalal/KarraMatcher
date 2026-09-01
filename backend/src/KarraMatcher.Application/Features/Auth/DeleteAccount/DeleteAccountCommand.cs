using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Auth.DeleteAccount;

/// <summary>Raderar kontot och allt servern äger om det.</summary>
public sealed record DeleteAccountCommand(Guid AccountId) : ICommand<Unit>;
