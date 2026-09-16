namespace KarraMatcher.Application.Features.Applications;

/// <summary>
/// Det ansökningssidan visar innan man skickar in (`#194`). Anonymt läsbar med trupp-id i
/// länken — bara truppens namn, inget om medlemmar eller barn.
/// </summary>
/// <param name="TruppName">Truppens namn, så den sökande ser att det stämmer.</param>
public sealed record ApplyInfoDto(string TruppName);
