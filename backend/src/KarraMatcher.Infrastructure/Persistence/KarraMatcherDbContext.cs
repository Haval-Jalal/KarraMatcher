using System.Reflection;

using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Chat;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence;

public sealed class KarraMatcherDbContext(DbContextOptions<KarraMatcherDbContext> options)
    : DbContext(options)
{
    public DbSet<Sport> Sports => Set<Sport>();

    public DbSet<Club> Clubs => Set<Club>();

    public DbSet<AgeGroup> AgeGroups => Set<AgeGroup>();

    public DbSet<Child> Children => Set<Child>();

    public DbSet<Guardianship> Guardianships => Set<Guardianship>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Venue> Venues => Set<Venue>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<LoginCode> LoginCodes => Set<LoginCode>();

    public DbSet<TeamRole> TeamRoles => Set<TeamRole>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<CarpoolOffer> CarpoolOffers => Set<CarpoolOffer>();

    public DbSet<CarpoolRequest> CarpoolRequests => Set<CarpoolRequest>();

    public DbSet<AttendanceCall> AttendanceCalls => Set<AttendanceCall>();

    public DbSet<AttendanceInvitation> AttendanceInvitations => Set<AttendanceInvitation>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<ChatReport> ChatReports => Set<ChatReport>();

    public DbSet<Domain.Push.PushSubscription> PushSubscriptions =>
        Set<Domain.Push.PushSubscription>();

    public DbSet<Domain.Push.NotificationPreference> NotificationPreferences =>
        Set<Domain.Push.NotificationPreference>();

    public DbSet<Domain.Invitations.Invitation> Invitations => Set<Domain.Invitations.Invitation>();

    public DbSet<Domain.Applications.MembershipApplication> MembershipApplications =>
        Set<Domain.Applications.MembershipApplication>();

    public DbSet<Domain.Consent.GuardianConsent> GuardianConsents =>
        Set<Domain.Consent.GuardianConsent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
