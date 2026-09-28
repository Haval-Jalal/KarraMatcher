using System.Reflection;
using KarraMatcher.Application.Abstractions.Security;
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

public sealed class KarraMatcherDbContext(
    DbContextOptions<KarraMatcherDbContext> options,
    IChatTextCipher chatCipher)
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

    public DbSet<ChatReaction> ChatReactions => Set<ChatReaction>();

    public DbSet<Domain.Push.PushSubscription> PushSubscriptions =>
        Set<Domain.Push.PushSubscription>();

    public DbSet<Domain.Invitations.Invitation> Invitations => Set<Domain.Invitations.Invitation>();

    public DbSet<Domain.Applications.MembershipApplication> MembershipApplications =>
        Set<Domain.Applications.MembershipApplication>();

    public DbSet<Domain.Consent.GuardianConsent> GuardianConsents =>
        Set<Domain.Consent.GuardianConsent>();

    public DbSet<Domain.Calendar.CalendarToken> CalendarTokens =>
        Set<Domain.Calendar.CalendarToken>();

    public DbSet<Domain.Accounts.Passkey> Passkeys => Set<Domain.Accounts.Passkey>();

    public DbSet<Domain.Cup.CupTeam> CupTeams => Set<Domain.Cup.CupTeam>();

    public DbSet<Domain.Cup.CupTeamMember> CupTeamMembers => Set<Domain.Cup.CupTeamMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Chattens fritext krypteras i vila (§KM.10). Kolumnerna är `text` (obegränsade) eftersom
        // chiffret är längre än klartexten; inmatningslängden vaktas i validatorn, inte här. Läggs
        // efter konfigurationerna så konverteraren gäller.
        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.Body)
            .HasConversion(value => chatCipher.Encrypt(value), value => chatCipher.Decrypt(value));
        modelBuilder.Entity<ChatReport>()
            .Property(r => r.Reason)
            .HasConversion(value => chatCipher.Encrypt(value), value => chatCipher.Decrypt(value));

        base.OnModelCreating(modelBuilder);
    }
}
