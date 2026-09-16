using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Administration;
using KarraMatcher.Domain.Applications;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Application.Features.Applications;

/// <summary>
/// Ansökningarnas livscykel (`#194`, §KM.3): förälder ansöker, admin godkänner eller nekar.
///
/// <para>
/// En godkänd ansökan är förälderns medlemskap i truppen — samma modell som en accepterad
/// inbjudan (`#193`), läst av <c>MembershipService</c>. Varje åtgärd audit-loggas med id,
/// aldrig adress (§KM.10). Inget om barn hanteras här (§KM.1, `#196`).
/// </para>
/// </summary>
public sealed class ApplicationService(
    IApplicationRepository applications,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<AdminOutcome> ApplyAsync(
        Guid ageGroupId, Guid accountId, CancellationToken cancellationToken)
    {
        if (!await applications.TruppExistsAsync(ageGroupId, cancellationToken).ConfigureAwait(false))
        {
            return AdminOutcome.NotFound;
        }

        // En väntande eller redan godkänd ansökan hindrar en ny — annars fylls kön av
        // dubbletter, och en redan medlem har inget att ansöka om.
        if (await applications.HasOpenForAsync(accountId, ageGroupId, cancellationToken)
            .ConfigureAwait(false))
        {
            return AdminOutcome.Conflict;
        }

        var application = new MembershipApplication
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroupId,
            AccountId = accountId,
            Status = ApplicationStatus.Pending,
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
        };

        await applications.AddAsync(application, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            AuditActions.ApplicationSubmitted, accountId, cancellationToken, application.Id)
            .ConfigureAwait(false);
        await applications.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminOutcome.Success;
    }

    public Task<AdminOutcome> ApproveAsync(
        Guid ageGroupId, Guid id, Guid actorAccountId, CancellationToken cancellationToken) =>
        ResolveAsync(
            ageGroupId, id, actorAccountId, ApplicationStatus.Approved,
            AuditActions.ApplicationApproved, cancellationToken);

    public Task<AdminOutcome> DenyAsync(
        Guid ageGroupId, Guid id, Guid actorAccountId, CancellationToken cancellationToken) =>
        ResolveAsync(
            ageGroupId, id, actorAccountId, ApplicationStatus.Denied,
            AuditActions.ApplicationDenied, cancellationToken);

    public async Task<ApplyInfoDto?> ApplyInfoAsync(
        Guid ageGroupId, CancellationToken cancellationToken)
    {
        var trupp = await applications.FindTruppAsync(ageGroupId, cancellationToken)
            .ConfigureAwait(false);

        return trupp is null ? null : new ApplyInfoDto(trupp.Name);
    }

    private async Task<AdminOutcome> ResolveAsync(
        Guid ageGroupId,
        Guid id,
        Guid actorAccountId,
        ApplicationStatus status,
        string action,
        CancellationToken cancellationToken)
    {
        var application = await applications.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);

        // Objektnivå: ansökan måste höra till truppen i adressen (som policyn gav åtkomst till).
        if (application is null || application.AgeGroupId != ageGroupId)
        {
            return AdminOutcome.NotFound;
        }

        // Bara en väntande ansökan går att avgöra — inte en redan avgjord.
        if (application.Status != ApplicationStatus.Pending)
        {
            return AdminOutcome.Conflict;
        }

        application.Status = status;
        application.ResolvedUtc = clock.GetUtcNow().UtcDateTime;
        application.ResolvedByAccountId = actorAccountId;

        await audit.RecordAsync(action, actorAccountId, cancellationToken, application.Id)
            .ConfigureAwait(false);
        await applications.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminOutcome.Success;
    }
}
