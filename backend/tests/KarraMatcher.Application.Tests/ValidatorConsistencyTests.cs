using KarraMatcher.Application.Features.Auth.DeleteAccount;
using KarraMatcher.Application.Features.Calendar;
using KarraMatcher.Application.Features.Carpool;
using KarraMatcher.Application.Features.Events.Admin;
using KarraMatcher.Application.Features.Invitations;
using KarraMatcher.Application.Features.Passkeys;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Validator-/korrekthetssvepet (`#612`). De här commandsen och queriesen bär bara id:n eller en
/// token, och saknade tidigare validator helt — så <c>CommandValidationBehavior</c> hoppade tyst
/// över dem. Ett tomt Guid eller en tom token ska avvisas med 400, inte tyst bli en 404.
/// </summary>
public class ValidatorConsistencyTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private const string Slug = "gul";
    private const string Token = "abc123";

    // ---- Token-queries (anonyma) -----------------------------------------------------

    [Fact]
    public void KalenderFeed_MedToken_ArGiltig()
    {
        Assert.True(new GetCalendarFeedQueryValidator()
            .Validate(new GetCalendarFeedQuery(Token)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void KalenderFeed_UtanToken_ArOgiltig(string token)
    {
        Assert.False(new GetCalendarFeedQueryValidator()
            .Validate(new GetCalendarFeedQuery(token)).IsValid);
    }

    [Fact]
    public void KalenderFeed_OrimligtLangToken_ArOgiltig()
    {
        Assert.False(new GetCalendarFeedQueryValidator()
            .Validate(new GetCalendarFeedQuery(new string('a', 129))).IsValid);
    }

    [Fact]
    public void Inbjudan_UtanToken_ArOgiltig()
    {
        Assert.False(new PreviewInvitationQueryValidator()
            .Validate(new PreviewInvitationQuery("")).IsValid);
    }

    [Fact]
    public void Inbjudan_MedToken_ArGiltig()
    {
        Assert.True(new PreviewInvitationQueryValidator()
            .Validate(new PreviewInvitationQuery(Token)).IsValid);
    }

    // ---- Händelse-commands (slug-bärande) --------------------------------------------

    [Fact]
    public void StallInMatch_MedSluggOchIder_ArGiltig()
    {
        Assert.True(new CancelEventCommandValidator()
            .Validate(new CancelEventCommand(Slug, Id, Id)).IsValid);
    }

    [Fact]
    public void StallInMatch_UtanSlug_ArOgiltig()
    {
        Assert.False(new CancelEventCommandValidator()
            .Validate(new CancelEventCommand("", Id, Id)).IsValid);
    }

    [Fact]
    public void StallInMatch_MedOtillatenSlug_ArOgiltig()
    {
        // Sluggen är användarindata ur URL:en: bara små bokstäver, siffror och bindestreck.
        Assert.False(new CancelEventCommandValidator()
            .Validate(new CancelEventCommand("Gul Laget!", Id, Id)).IsValid);
    }

    [Fact]
    public void TaBortMatch_UtanHandelseId_ArOgiltig()
    {
        Assert.False(new DeleteEventCommandValidator()
            .Validate(new DeleteEventCommand(Slug, Guid.Empty, Id)).IsValid);
    }

    [Fact]
    public void StallInTruppHandelse_UtanTrupp_ArOgiltig()
    {
        Assert.False(new CancelTruppEventCommandValidator()
            .Validate(new CancelTruppEventCommand(Guid.Empty, Id, Id)).IsValid);
    }

    [Fact]
    public void TaBortTruppHandelse_MedIder_ArGiltig()
    {
        Assert.True(new DeleteTruppEventCommandValidator()
            .Validate(new DeleteTruppEventCommand(Id, Id, Id)).IsValid);
    }

    // ---- Konto-/nyckel-/samåknings-commands ------------------------------------------

    [Fact]
    public void NyKalendernyckel_UtanKonto_ArOgiltig()
    {
        Assert.False(new RegenerateCalendarTokenCommandValidator()
            .Validate(new RegenerateCalendarTokenCommand(Guid.Empty)).IsValid);
    }

    [Fact]
    public void RaderaKonto_UtanKonto_ArOgiltig()
    {
        Assert.False(new DeleteAccountCommandValidator()
            .Validate(new DeleteAccountCommand(Guid.Empty)).IsValid);
    }

    [Fact]
    public void TaBortPasskey_UtanNyckel_ArOgiltig()
    {
        Assert.False(new RemovePasskeyCommandValidator()
            .Validate(new RemovePasskeyCommand(Id, Guid.Empty)).IsValid);
    }

    [Fact]
    public void DraTillbakaErbjudande_UtanErbjudande_ArOgiltig()
    {
        Assert.False(new WithdrawCarpoolOfferCommandValidator()
            .Validate(new WithdrawCarpoolOfferCommand(Guid.Empty, Id)).IsValid);
    }

    [Fact]
    public void AtertaForfragan_MedIder_ArGiltig()
    {
        Assert.True(new RetractCarpoolRequestCommandValidator()
            .Validate(new RetractCarpoolRequestCommand(Id, Id)).IsValid);
    }
}
