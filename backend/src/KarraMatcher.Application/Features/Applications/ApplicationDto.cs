namespace KarraMatcher.Application.Features.Applications;

/// <summary>
/// En ansökan som admin-kön visar den (`#194`).
///
/// <para>
/// Namn och adress är den sökande <b>vuxnas</b> egna uppgifter (§KM.1 rör barn) och visas
/// bara för truppens admin, aldrig i loggar (§KM.10). Inget om barn — ansökan är vuxen↔trupp.
/// </para>
/// </summary>
/// <param name="Id">Ansökans id.</param>
/// <param name="ApplicantName">Den sökandes visningsnamn, eller null om hen inte fyllt i något.</param>
/// <param name="ApplicantEmail">Den sökandes adress, så admin ser vem det är.</param>
/// <param name="Status">Väntar / Godkänd / Nekad.</param>
/// <param name="CreatedUtc">När ansökan skickades in.</param>
public sealed record ApplicationDto(
    Guid Id,
    string? ApplicantName,
    string ApplicantEmail,
    string Status,
    DateTimeOffset CreatedUtc);
