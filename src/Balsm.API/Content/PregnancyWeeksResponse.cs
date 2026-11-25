using Balsm.SharedKernel.Media;

namespace Balsm.API.Content;

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
public sealed record FetalSizeDto(int? LengthMm, int? WeightG, string? Comparison);

/// <summary>Who made a set of illustrations, and under which licence they are shown.</summary>
/// <param name="Author">Maker of the artwork.</param>
/// <param name="License">Licence name, e.g. CC BY-ND 4.0.</param>
/// <param name="LicenseUrl">The licence text.</param>
/// <param name="AuthorUrl">The maker's site.</param>
/// <param name="Text">Credit line shown under the artwork.</param>
public sealed record IllustrationCreditDto(string Author, string License, string LicenseUrl, string AuthorUrl, string Text);

/// <summary>The baby's side of a week.</summary>
/// <param name="Fact">One line on how the baby is developing.</param>
/// <param name="FetalSize">The baby's typical size this week.</param>
/// <param name="Media">
/// The week's artwork with its light and dark renditions, or null when it has none. <c>media.credit</c>
/// is a key of <c>illustration_credits</c>.
/// </param>
/// <param name="Sources">Ids from the top-level <c>sources</c> backing the baby's copy.</param>
public sealed record BabyDto(string Fact, FetalSizeDto FetalSize, MediaDto? Media, IReadOnlyList<string> Sources);

/// <summary>A common pregnancy symptom: its name, what helps, and when to call the doctor.</summary>
/// <param name="Label">The symptom's name.</param>
/// <param name="SelfCare">What helps.</param>
/// <param name="CallDoctorIf">When to call the doctor about it; null when the sources name nothing.</param>
/// <param name="Sources">Ids from the top-level <c>sources</c> backing it.</param>
public sealed record SymptomDto(string Label, string SelfCare, string? CallDoctorIf, IReadOnlyList<string> Sources);

/// <summary>Guidance for one week of pregnancy.</summary>
/// <param name="Week">Gestational week, 4 to 40 inclusive.</param>
/// <param name="Title">Short heading for the week.</param>
/// <param name="Summary">Two or three lines on the mother's week.</param>
/// <param name="Symptoms">Keys of <c>symptom_guide</c> common this week.</param>
/// <param name="Feelings">How she may feel this week.</param>
/// <param name="Tips">One or two things to do this week.</param>
/// <param name="TestsDue">Keys of <c>test_labels</c> for checks usually due this week.</param>
/// <param name="DangerSigns">Keys of <c>danger_sign_labels</c> to call the doctor about.</param>
/// <param name="Topics">Keys of <c>topic_labels</c> (nutrition, visits, movements, …).</param>
/// <param name="Sources">Ids from the top-level <c>sources</c> backing the mother's copy.</param>
/// <param name="Baby">The baby's side of the week: fact, size, artwork.</param>
public sealed record PregnancyWeekDto(
    int Week,
    string Title,
    string Summary,
    IReadOnlyList<string> Symptoms,
    string Feelings,
    IReadOnlyList<string> Tips,
    IReadOnlyList<string> TestsDue,
    IReadOnlyList<string> DangerSigns,
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> Sources,
    BabyDto Baby);

/// <summary>
/// Week-by-week pregnancy guidance in one language. General information only — never patient
/// data — so one document per language serves every caller.
/// </summary>
/// <param name="Language">The language every text in the document is in: <c>en</c> or <c>ar</c>.</param>
/// <param name="Version">Content version; changes whenever the copy changes.</param>
/// <param name="Source">Where the content comes from, shown after the disclaimer ("… Based on WHO and Egypt MOHP guidance.").</param>
/// <param name="Disclaimer">"General information, not medical advice." line shown with the content.</param>
/// <param name="MultiplesNote">Shown alongside the content when the pregnancy carries more than one baby.</param>
/// <param name="Labels">
/// The guide's own wording (section headings, size phrases, units, the sources link), keyed by name, so the
/// app draws it from the content rather than its own strings. Placeholders in braces — <c>{n}</c>,
/// <c>{thing}</c>, <c>{value}</c>, <c>{week}</c> — are filled in by the app.
/// </param>
/// <param name="Sources">Every source the weeks and symptoms cite.</param>
/// <param name="SymptomGuide">Each symptom key's name, self-care and when to call the doctor, defined once.</param>
/// <param name="DangerSignLabels">Wording for each danger-sign key, defined once.</param>
/// <param name="TestLabels">Wording for each test key, defined once.</param>
/// <param name="TopicLabels">Wording for each <c>topics</c> key, defined once.</param>
/// <param name="IllustrationCredits">Credit for each <c>media.credit</c> key, defined once.</param>
/// <param name="Weeks">One entry per week, ascending.</param>
public sealed record PregnancyWeeksDto(
    string Language,
    string Version,
    string Source,
    string Disclaimer,
    string MultiplesNote,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<ContentSourceDto> Sources,
    IReadOnlyDictionary<string, SymptomDto> SymptomGuide,
    IReadOnlyDictionary<string, string> DangerSignLabels,
    IReadOnlyDictionary<string, string> TestLabels,
    IReadOnlyDictionary<string, string> TopicLabels,
    IReadOnlyDictionary<string, IllustrationCreditDto> IllustrationCredits,
    IReadOnlyList<PregnancyWeekDto> Weeks);

/// <summary>Response envelope for <c>GET /api/v1/content/pregnancy-weeks</c>.</summary>
/// <param name="Data">The guidance document in the requested language.</param>
public sealed record PregnancyWeeksResponse(PregnancyWeeksDto Data);
