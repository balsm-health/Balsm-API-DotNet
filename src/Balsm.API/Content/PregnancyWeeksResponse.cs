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

/// <summary>Artwork for a week, credited to its maker.</summary>
/// <param name="Key">Artwork key, e.g. <c>pregnancy/week-12</c>; the app ships the image under it.</param>
/// <param name="Credit">Key of <c>illustration_credits</c> naming who made it.</param>
/// <param name="SourceUrl">Where the artwork was published.</param>
public sealed record IllustrationDto(string Key, string Credit, string SourceUrl);

/// <summary>Who made a set of illustrations, and under which licence they are shown.</summary>
/// <param name="Author">Maker of the artwork.</param>
/// <param name="License">Licence name, e.g. CC BY-ND 4.0.</param>
/// <param name="LicenseUrl">The licence text.</param>
/// <param name="AuthorUrl">The maker's site.</param>
/// <param name="Text">Credit line shown under the artwork.</param>
public sealed record IllustrationCreditDto(string Author, string License, string LicenseUrl, string AuthorUrl, LocalizedText Text);

/// <summary>Guidance for one week of pregnancy.</summary>
/// <param name="Week">Gestational week, 4 to 40 inclusive.</param>
/// <param name="Title">Short heading for the week.</param>
/// <param name="Body">Two or three lines of general, non-personal information.</param>
/// <param name="FetalSize">The baby's typical size this week.</param>
/// <param name="TestsDue">Keys of <c>test_labels</c> for checks usually due this week.</param>
/// <param name="DangerSigns">Keys of <c>danger_sign_labels</c> the body warns about.</param>
/// <param name="Topics">Keys of <c>topic_labels</c> (nutrition, visits, movements, …).</param>
/// <param name="Illustration">The week's artwork, or null when it has none.</param>
/// <param name="Sources">Ids from the top-level <c>sources</c> backing this week's copy.</param>
public sealed record PregnancyWeekDto(
    int Week,
    LocalizedText Title,
    LocalizedText Body,
    FetalSizeDto FetalSize,
    IReadOnlyList<string> TestsDue,
    IReadOnlyList<string> DangerSigns,
    IReadOnlyList<string> Topics,
    IllustrationDto? Illustration,
    IReadOnlyList<string> Sources);

/// <summary>
/// Week-by-week pregnancy guidance. General information only — never patient
/// data — so one document serves every caller.
/// </summary>
/// <param name="Version">Content version; changes whenever the copy changes.</param>
/// <param name="Source">Where the content comes from, shown after the disclaimer ("… Based on WHO and Egypt MOHP guidance.").</param>
/// <param name="Disclaimer">"General information, not medical advice." line shown with the content.</param>
/// <param name="MultiplesNote">Shown alongside the content when the pregnancy carries more than one baby.</param>
/// <param name="Labels">
/// The guide's own wording (section headings, size phrases, units, the sources link), keyed by name, so the
/// app draws it from the content rather than its own strings. Placeholders in braces — <c>{n}</c>,
/// <c>{thing}</c>, <c>{value}</c>, <c>{week}</c> — are filled in by the app.
/// </param>
/// <param name="Sources">Every source the weeks cite.</param>
/// <param name="DangerSignLabels">Wording for each danger-sign key, defined once.</param>
/// <param name="TestLabels">Wording for each test key, defined once.</param>
/// <param name="TopicLabels">Wording for each <c>topics</c> key, defined once.</param>
/// <param name="IllustrationCredits">Credit for each <c>illustration.credit</c> key, defined once.</param>
/// <param name="Weeks">One entry per week, ascending.</param>
public sealed record PregnancyWeeksDto(
    string Version,
    LocalizedText Source,
    LocalizedText Disclaimer,
    LocalizedText MultiplesNote,
    IReadOnlyDictionary<string, LocalizedText> Labels,
    IReadOnlyList<ContentSourceDto> Sources,
    IReadOnlyDictionary<string, LocalizedText> DangerSignLabels,
    IReadOnlyDictionary<string, LocalizedText> TestLabels,
    IReadOnlyDictionary<string, LocalizedText> TopicLabels,
    IReadOnlyDictionary<string, IllustrationCreditDto> IllustrationCredits,
    IReadOnlyList<PregnancyWeekDto> Weeks);

/// <summary>Response envelope for <c>GET /api/v1/content/pregnancy-weeks</c>.</summary>
/// <param name="Data">The guidance document.</param>
public sealed record PregnancyWeeksResponse(PregnancyWeeksDto Data);
