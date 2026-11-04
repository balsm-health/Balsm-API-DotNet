namespace Balsm.API.Content;

/// <summary>A piece of patient-facing copy in every language the app ships.</summary>
/// <param name="En">English text.</param>
/// <param name="Ar">Arabic text.</param>
public sealed record LocalizedText(string En, string Ar);

/// <summary>Guidance for one week of pregnancy.</summary>
/// <param name="Week">Gestational week, 4 to 40 inclusive.</param>
/// <param name="Title">Short heading for the week.</param>
/// <param name="Body">Two or three lines of general, non-personal information.</param>
public sealed record PregnancyWeekDto(int Week, LocalizedText Title, LocalizedText Body);

/// <summary>
/// Week-by-week pregnancy guidance. General information only — never patient
/// data — so one document serves every caller.
/// </summary>
/// <param name="Version">Content version; changes whenever the copy changes.</param>
/// <param name="Source">Organisation the content comes from, shown as "Content from …".</param>
/// <param name="Disclaimer">"General information, not medical advice." line shown with the content.</param>
/// <param name="MultiplesNote">Shown alongside the content when the pregnancy carries more than one baby.</param>
/// <param name="Weeks">One entry per week, ascending.</param>
public sealed record PregnancyWeeksDto(
    string Version,
    LocalizedText Source,
    LocalizedText Disclaimer,
    LocalizedText MultiplesNote,
    IReadOnlyList<PregnancyWeekDto> Weeks);

/// <summary>Response envelope for <c>GET /api/v1/content/pregnancy-weeks</c>.</summary>
/// <param name="Data">The guidance document.</param>
public sealed record PregnancyWeeksResponse(PregnancyWeeksDto Data);
