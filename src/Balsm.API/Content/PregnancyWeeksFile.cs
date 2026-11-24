namespace Balsm.API.Content;

/// <summary>A piece of patient-facing copy in every language the content is written in.</summary>
/// <param name="En">English text.</param>
/// <param name="Ar">Arabic text.</param>
public sealed record LocalizedText(string En, string Ar)
{
    /// <summary>The text in <paramref name="language"/> (<c>en</c> or <c>ar</c>).</summary>
    public string In(string language) => language == PregnancyWeeksContent.Arabic ? Ar : En;
}

/// <summary>The baby's typical size in a week, as written. Each figure is null where the sources give none.</summary>
/// <param name="LengthMm">Length in millimetres, head to bottom.</param>
/// <param name="WeightG">Weight in grams.</param>
/// <param name="Comparison">Everyday object of about that size, e.g. "a plum".</param>
public sealed record FetalSizeEntry(int? LengthMm, int? WeightG, LocalizedText? Comparison);

/// <summary>Who made a set of illustrations, as written.</summary>
/// <param name="Author">Maker of the artwork.</param>
/// <param name="License">Licence name, e.g. CC BY-ND 4.0.</param>
/// <param name="LicenseUrl">The licence text.</param>
/// <param name="AuthorUrl">The maker's site.</param>
/// <param name="Text">Credit line shown under the artwork.</param>
public sealed record IllustrationCreditEntry(string Author, string License, string LicenseUrl, string AuthorUrl, LocalizedText Text);

/// <summary>One week as written in the content file.</summary>
/// <param name="Week">Gestational week, 4 to 40 inclusive.</param>
/// <param name="Title">Short heading for the week.</param>
/// <param name="Body">Two or three lines of general, non-personal information.</param>
/// <param name="FetalSize">The baby's typical size this week.</param>
/// <param name="TestsDue">Keys of <c>test_labels</c> for checks usually due this week.</param>
/// <param name="DangerSigns">Keys of <c>danger_sign_labels</c> the body warns about.</param>
/// <param name="Topics">Keys of <c>topic_labels</c>.</param>
/// <param name="Illustration">The week's artwork, or null when it has none.</param>
/// <param name="Sources">Ids from the top-level <c>sources</c> backing this week's copy.</param>
public sealed record PregnancyWeekEntry(
    int Week,
    LocalizedText Title,
    LocalizedText Body,
    FetalSizeEntry FetalSize,
    IReadOnlyList<string> TestsDue,
    IReadOnlyList<string> DangerSigns,
    IReadOnlyList<string> Topics,
    IllustrationDto? Illustration,
    IReadOnlyList<string> Sources);

/// <summary>
/// The content file (<c>Content/pregnancy-weeks.v1.json</c>) as authors write it: every piece of
/// copy in both languages side by side. The API serves one language of it per request
/// (<see cref="PregnancyWeeksDto"/>), never this shape.
/// </summary>
/// <param name="Version">Content version; changes whenever the copy changes.</param>
/// <param name="Source">Where the content comes from, shown after the disclaimer.</param>
/// <param name="Disclaimer">"General information, not medical advice."</param>
/// <param name="MultiplesNote">Shown when the pregnancy carries more than one baby.</param>
/// <param name="Labels">The guide's own wording, keyed by name, with <c>{n}</c>-style placeholders.</param>
/// <param name="Sources">Every source the weeks cite.</param>
/// <param name="DangerSignLabels">Wording for each danger-sign key.</param>
/// <param name="TestLabels">Wording for each test key.</param>
/// <param name="TopicLabels">Wording for each topic key.</param>
/// <param name="IllustrationCredits">Credit for each <c>illustration.credit</c> key.</param>
/// <param name="Weeks">One entry per week, ascending.</param>
public sealed record PregnancyWeeksFile(
    string Version,
    LocalizedText Source,
    LocalizedText Disclaimer,
    LocalizedText MultiplesNote,
    IReadOnlyDictionary<string, LocalizedText> Labels,
    IReadOnlyList<ContentSourceDto> Sources,
    IReadOnlyDictionary<string, LocalizedText> DangerSignLabels,
    IReadOnlyDictionary<string, LocalizedText> TestLabels,
    IReadOnlyDictionary<string, LocalizedText> TopicLabels,
    IReadOnlyDictionary<string, IllustrationCreditEntry> IllustrationCredits,
    IReadOnlyList<PregnancyWeekEntry> Weeks)
{
    /// <summary>The whole document in <paramref name="language"/>: every text field becomes a plain string.</summary>
    public PregnancyWeeksDto In(string language)
    {
        IReadOnlyDictionary<string, string> Flat(IReadOnlyDictionary<string, LocalizedText> texts) =>
            texts.ToDictionary(e => e.Key, e => e.Value.In(language));

        return new PregnancyWeeksDto(
            language,
            Version,
            Source.In(language),
            Disclaimer.In(language),
            MultiplesNote.In(language),
            Flat(Labels),
            Sources,
            Flat(DangerSignLabels),
            Flat(TestLabels),
            Flat(TopicLabels),
            IllustrationCredits.ToDictionary(
                e => e.Key,
                e => new IllustrationCreditDto(e.Value.Author, e.Value.License, e.Value.LicenseUrl, e.Value.AuthorUrl, e.Value.Text.In(language))),
            Weeks.Select(w => new PregnancyWeekDto(
                w.Week,
                w.Title.In(language),
                w.Body.In(language),
                new FetalSizeDto(w.FetalSize.LengthMm, w.FetalSize.WeightG, w.FetalSize.Comparison?.In(language)),
                w.TestsDue,
                w.DangerSigns,
                w.Topics,
                w.Illustration,
                w.Sources)).ToList());
    }
}
