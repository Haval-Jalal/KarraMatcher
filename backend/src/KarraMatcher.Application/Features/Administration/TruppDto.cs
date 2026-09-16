namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// En trupp (kodnamn <c>AgeGroup</c>) så som superadmin-konsolen visar den (§KM.3, `#192`).
///
/// <para>
/// En trupp hör till <em>en klubb</em> och <em>en sport</em>, och samlar lagen. Den har
/// ingen egen slug — den identifieras av klubb + namn + säsong.
/// </para>
/// </summary>
/// <param name="Id">Truppens id.</param>
/// <param name="ClubId">Klubben truppen hör till.</param>
/// <param name="ClubName">Klubbens namn, för visning.</param>
/// <param name="SportId">Sporten truppen tillhör.</param>
/// <param name="SportName">Sportens namn, för visning.</param>
/// <param name="Name">Truppens namn, t.ex. "P2016".</param>
/// <param name="Season">Säsongen, t.ex. "2026".</param>
public sealed record TruppDto(
    Guid Id,
    Guid ClubId,
    string ClubName,
    Guid SportId,
    string SportName,
    string Name,
    string Season);
