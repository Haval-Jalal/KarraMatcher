using System.Text.Json;

using Fido2NetLib;
using Fido2NetLib.Objects;

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Passkeys;
using KarraMatcher.Domain.Accounts;

using Microsoft.Extensions.Caching.Memory;

namespace KarraMatcher.Infrastructure.Security;

/// <summary>
/// WebAuthn-ceremonierna med Fido2NetLib. Utmaningen mellan <c>Begin</c> och <c>Complete</c>
/// lagras kortlivat i minnet (enkel-instans på Render räcker — en tappad utmaning betyder bara
/// att man får trycka igen).
/// </summary>
internal sealed class PasskeyCeremony(
    IFido2 fido2,
    IPasskeyRepository passkeys,
    IAccountRepository accounts,
    IMemoryCache challenges,
    TimeProvider clock) : IPasskeyCeremony
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    public async Task<string> BeginRegistrationAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accounts.FindByIdAsync(accountId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Inloggat konto saknas vid passkey-registrering.");

        var existing = await passkeys.CredentialIdsForAccountAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                Id = accountId.ToByteArray(),
                Name = account.Email,
                DisplayName = account.DisplayName ?? account.Email,
            },
            ExcludeCredentials =
                [.. existing.Select(id => new PublicKeyCredentialDescriptor(id))],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                // Discoverable credential → usernameless: man trycker bara "logga in".
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Preferred,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });

        var json = options.ToJson();
        challenges.Set(RegistrationKey(accountId), json, ChallengeLifetime);

        return json;
    }

    public async Task<PasskeyRegistrationOutcome> CompleteRegistrationAsync(
        Guid accountId,
        string attestationJson,
        string? deviceLabel,
        CancellationToken cancellationToken)
    {
        if (challenges.Get<string>(RegistrationKey(accountId)) is not { } optionsJson)
        {
            return PasskeyRegistrationOutcome.Invalid;
        }

        var options = CredentialCreateOptions.FromJson(optionsJson);

        if (JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationJson)
            is not { } attestation)
        {
            return PasskeyRegistrationOutcome.Invalid;
        }

        RegisteredPublicKeyCredential credential;

        try
        {
            credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestation,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = async (args, token) =>
                    !await passkeys.ExistsAsync(args.CredentialId, token).ConfigureAwait(false),
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Fido2VerificationException)
        {
            return PasskeyRegistrationOutcome.Invalid;
        }

        challenges.Remove(RegistrationKey(accountId));

        await passkeys.AddAsync(
            new Passkey
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                CredentialId = credential.Id,
                PublicKey = credential.PublicKey,
                SignCount = credential.SignCount,
                DeviceLabel = string.IsNullOrWhiteSpace(deviceLabel) ? null : deviceLabel.Trim(),
                CreatedUtc = clock.GetUtcNow().UtcDateTime,
            },
            cancellationToken).ConfigureAwait(false);

        await passkeys.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return PasskeyRegistrationOutcome.Registered;
    }

    public Task<PasskeyLoginChallenge> BeginLoginAsync(CancellationToken cancellationToken)
    {
        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            // Tom lista → usernameless: enheten väljer själv rätt discoverable credential.
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Preferred,
        });

        var challengeId = Guid.NewGuid().ToString("N");
        challenges.Set(LoginKey(challengeId), options.ToJson(), ChallengeLifetime);

        return Task.FromResult(new PasskeyLoginChallenge(challengeId, options.ToJson()));
    }

    public async Task<Guid?> CompleteLoginAsync(
        string challengeId,
        string assertionJson,
        CancellationToken cancellationToken)
    {
        if (challenges.Get<string>(LoginKey(challengeId)) is not { } optionsJson)
        {
            return null;
        }

        challenges.Remove(LoginKey(challengeId));

        var options = AssertionOptions.FromJson(optionsJson);

        if (JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(assertionJson)
            is not { } assertion)
        {
            return null;
        }

        var passkey = await passkeys.FindByCredentialIdAsync(assertion.RawId, cancellationToken)
            .ConfigureAwait(false);

        if (passkey is null)
        {
            return null;
        }

        try
        {
            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertion,
                OriginalOptions = options,
                StoredPublicKey = passkey.PublicKey,
                StoredSignatureCounter = (uint)passkey.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                    Task.FromResult(
                        args.UserHandle.Length == 16
                        && new Guid(args.UserHandle) == passkey.AccountId),
            }, cancellationToken).ConfigureAwait(false);

            passkey.SignCount = result.SignCount;
            passkey.LastUsedUtc = clock.GetUtcNow().UtcDateTime;
            await passkeys.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return passkey.AccountId;
        }
        catch (Fido2VerificationException)
        {
            return null;
        }
    }

    private static string RegistrationKey(Guid accountId) => $"passkey-reg-{accountId:N}";

    private static string LoginKey(string challengeId) => $"passkey-login-{challengeId}";
}
