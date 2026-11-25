using Balsm.SharedKernel.Media;

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

/// <summary>One value a user axis can take, as written.</summary>
/// <param name="Value">The value as written in a rendition's <c>when</c>.</param>
/// <param name="Label">What the switch shows for it.</param>
public sealed record MediaOptionEntry(string Value, LocalizedText Label);

/// <summary>A dimension a media item's renditions vary along, as written.</summary>
/// <param name="Id">Axis name used as a key in each rendition's <c>when</c>.</param>
/// <param name="Source"><c>app</c> or <c>user</c>.</param>
/// <param name="Label">The switch's label; user axes only.</param>
/// <param name="Options">The values the user can pick; user axes only.</param>
/// <param name="Default">The option used until the user picks one; user axes only.</param>
public sealed record MediaAxisEntry(
    string Id,
    string Source,
    LocalizedText? Label = null,
    IReadOnlyList<MediaOptionEntry>? Options = null,
    string? Default = null);

/// <summary>A media item as written: its texts in both languages, its files once.</summary>
/// <param name="Kind">What the media is: <c>image</c>.</param>
/// <param name="Alt">What the media shows, for screen readers.</param>
/// <param name="Axes">The axes the renditions vary along.</param>
/// <param name="Renditions">Every file the media exists as.</param>
/// <param name="Credit">Key of <c>illustration_credits</c> naming who made it.</param>
/// <param name="SourceUrl">Where the media was first published.</param>
public sealed record MediaEntry(
    string Kind,
    LocalizedText Alt,
    IReadOnlyList<MediaAxisEntry> Axes,
    IReadOnlyList<MediaRenditionDto> Renditions,
    string? Credit = null,
    string? SourceUrl = null)
{
    /// <summary>The media with every text in <paramref name="language"/> and every rendition kept.</summary>
    public MediaDto Describe(string language) => new(
        Kind,
        Alt.In(language),
        Credit,
        SourceUrl,
        Axes.Select(a => new MediaAxisDto(
            a.Id,
            a.Source,
            a.Label?.In(language),
            a.Options?.Select(o => new MediaOptionDto(o.Value, o.Label.In(language))).ToList(),
            a.Default)).ToList(),
        Renditions);

    /// <summary>
    /// The media as served in <paramref name="language"/>: <see cref="Describe"/>, without the
    /// renditions made for another locale, so the app only receives files it can use.
    /// </summary>
    public MediaDto In(string language) => Describe(language) with
    {
        Renditions = Renditions.Where(r => !r.When.TryGetValue("locale", out var locale) || locale == language).ToList(),
    };
}

/// <summary>The baby's side of a week, as written.</summary>
/// <param name="Fact">One line on how the baby is developing.</param>
/// <param name="FetalSize">The baby's typical size this week.</param>
/// <param name="Media">The week's artwork, or null when it has none.</param>
/// <param name="Sources">Ids from the top-level <c>sources</c> backing the baby's copy.</param>
public sealed record BabyEntry(LocalizedText Fact, FetalSizeEntry FetalSize, MediaEntry? Media, IReadOnlyList<string> Sources);

/// <summary>A common pregnancy symptom, as written: its name, what helps, and when to call the doctor.</summary>
/// <param name="Label">The symptom's name.</param>
/// <param name="SelfCare">What helps.</param>
/// <param name="Sources">Ids from the top-level <c>sources</c> backing it.</param>
/// <param name="CallDoctorIf">When to call the doctor about it; null when the sources name nothing.</param>
public sealed record SymptomEntry(LocalizedText Label, LocalizedText SelfCare, IReadOnlyList<string> Sources, LocalizedText? CallDoctorIf = null);

/// <summary>One week as written in the content file.</summary>
/// <param name="Week">Gestational week, 4 to 40 inclusive.</param>
/// <param name="Title">Short heading for the week.</param>
/// <param name="Summary">Two or three lines on the mother's week.</param>
/// <param name="Symptoms">Keys of <c>symptom_guide</c> common this week.</param>
/// <param name="Feelings">How she may feel this week.</param>
/// <param name="Tips">One or two things to do this week.</param>
/// <param name="TestsDue">Keys of <c>test_labels</c> for checks usually due this week.</param>
/// <param name="DangerSigns">Keys of <c>danger_sign_labels</c> to call the doctor about.</param>
/// <param name="Topics">Keys of <c>topic_labels</c>.</param>
/// <param name="Sources">Ids from the top-level <c>sources</c> backing the mother's copy.</param>
/// <param name="Baby">The baby's side of the week.</param>
public sealed record PregnancyWeekEntry(
    int Week,
    LocalizedText Title,
    LocalizedText Summary,
    IReadOnlyList<string> Symptoms,
    LocalizedText Feelings,
    IReadOnlyList<LocalizedText> Tips,
    IReadOnlyList<string> TestsDue,
    IReadOnlyList<string> DangerSigns,
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> Sources,
    BabyEntry Baby);

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
/// <param name="Sources">Every source the weeks and symptoms cite.</param>
/// <param name="SymptomGuide">Each symptom key's name, self-care and when to call the doctor.</param>
/// <param name="DangerSignLabels">Wording for each danger-sign key.</param>
/// <param name="TestLabels">Wording for each test key.</param>
/// <param name="TopicLabels">Wording for each topic key.</param>
/// <param name="IllustrationCredits">Credit for each <c>media.credit</c> key.</param>
/// <param name="Weeks">One entry per week, ascending.</param>
public sealed record PregnancyWeeksFile(
    string Version,
    LocalizedText Source,
    LocalizedText Disclaimer,
    LocalizedText MultiplesNote,
    IReadOnlyDictionary<string, LocalizedText> Labels,
    IReadOnlyList<ContentSourceDto> Sources,
    IReadOnlyDictionary<string, SymptomEntry> SymptomGuide,
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
            SymptomGuide.ToDictionary(
                e => e.Key,
                e => new SymptomDto(e.Value.Label.In(language), e.Value.SelfCare.In(language), e.Value.CallDoctorIf?.In(language), e.Value.Sources)),
            Flat(DangerSignLabels),
            Flat(TestLabels),
            Flat(TopicLabels),
            IllustrationCredits.ToDictionary(
                e => e.Key,
                e => new IllustrationCreditDto(e.Value.Author, e.Value.License, e.Value.LicenseUrl, e.Value.AuthorUrl, e.Value.Text.In(language))),
            Weeks.Select(w => new PregnancyWeekDto(
                w.Week,
                w.Title.In(language),
                w.Summary.In(language),
                w.Symptoms,
                w.Feelings.In(language),
                w.Tips.Select(t => t.In(language)).ToList(),
                w.TestsDue,
                w.DangerSigns,
                w.Topics,
                w.Sources,
                new BabyDto(
                    w.Baby.Fact.In(language),
                    new FetalSizeDto(w.Baby.FetalSize.LengthMm, w.Baby.FetalSize.WeightG, w.Baby.FetalSize.Comparison?.In(language)),
                    w.Baby.Media?.In(language),
                    w.Baby.Sources))).ToList());
    }
}
