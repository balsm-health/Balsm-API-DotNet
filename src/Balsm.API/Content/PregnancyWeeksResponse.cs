namespace Balsm.API.Content;

/// <summary>A piece of patient-facing copy in every language the app ships.</summary>
/// <param name="En">English text.</param>
/// <param name="Ar">Arabic text.</param>
public sealed record LocalizedText(string En, string Ar);

/// <summary>A published source the guidance is drawn from.</summary>
/// <param name="Id">Short key the weeks cite in their <c>sources</c> list.</param>
/// <param name="Title">Title of the page or document.</param>
/// <param name="Publisher">Organisation that published it.</param>
/// <param name="Url">Where it was read.</param>
/// <param name="Accessed">Day it was read.</param>
public sealed record ContentSourceDto(string Id, string Title, string Publisher, string Url, DateOnly Accessed);

/// <summary>The baby's typical size in a week. Each figure is null where the sources give none.</summary>
/// <param name="LengthMm">Length in millimetres, head to bottom.</param>
/// <param name="WeightG">Weight in grams.</param>
/// <param name="Comparison">Everyday object of about that size, e.g. "a plum".</param>
public sealed record FetalSizeDto(int? LengthMm, int? WeightG, LocalizedText? Comparison);

/// <summary>Guidance for one week of pregnancy.</summary>
/// <param name="Week">Gestational week, 4 to 40 inclusive.</param>
/// <param name="Title">Short heading for the week.</param>
/// <param name="Body">Two or three lines of general, non-personal information.</param>
/// <param name="FetalSize">The baby's typical size this week.</param>
/// <param name="TestsDue">Keys of <c>test_labels</c> for checks usually due this week.</param>
/// <param name="DangerSigns">Keys of <c>danger_sign_labels</c> the body warns about.</param>
/// <param name="Topics">Subject tags (nutrition, visits, movements, …) for filtering.</param>
/// <param name="Illustration">Artwork key for the week, e.g. <c>pregnancy/week-12</c>.</param>
/// <param name="Sources">Ids from the top-level <c>sources</c> backing this week's copy.</param>
public sealed record PregnancyWeekDto(
    int Week,
    LocalizedText Title,
    LocalizedText Body,
    FetalSizeDto FetalSize,
    IReadOnlyList<string> TestsDue,
    IReadOnlyList<string> DangerSigns,
    IReadOnlyList<string> Topics,
    string Illustration,
    IReadOnlyList<string> Sources);

/// <summary>
/// Week-by-week pregnancy guidance. General information only — never patient
/// data — so one document serves every caller.
/// </summary>
/// <param name="Version">Content version; changes whenever the copy changes.</param>
/// <param name="Source">Organisation the content comes from, shown as "Content from …".</param>
/// <param name="Disclaimer">"General information, not medical advice." line shown with the content.</param>
/// <param name="MultiplesNote">Shown alongside the content when the pregnancy carries more than one baby.</param>
/// <param name="Sources">Every source the weeks cite.</param>
/// <param name="DangerSignLabels">Wording for each danger-sign key, defined once.</param>
/// <param name="TestLabels">Wording for each test key, defined once.</param>
/// <param name="Weeks">One entry per week, ascending.</param>
public sealed record PregnancyWeeksDto(
    string Version,
    LocalizedText Source,
    LocalizedText Disclaimer,
    LocalizedText MultiplesNote,
    IReadOnlyList<ContentSourceDto> Sources,
    IReadOnlyDictionary<string, LocalizedText> DangerSignLabels,
    IReadOnlyDictionary<string, LocalizedText> TestLabels,
    IReadOnlyList<PregnancyWeekDto> Weeks);

/// <summary>Response envelope for <c>GET /api/v1/content/pregnancy-weeks</c>.</summary>
/// <param name="Data">The guidance document.</param>
public sealed record PregnancyWeeksResponse(PregnancyWeeksDto Data);
