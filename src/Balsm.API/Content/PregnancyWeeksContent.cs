using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace Balsm.API.Content;

/// <summary>
/// The week-by-week pregnancy guidance shipped inside the API assembly as
/// <c>Content/pregnancy-weeks.v1.json</c>, written in English and Arabic side by
/// side. Read, validated and serialised once per language; a request gets only
/// its language — half the bytes of both — with that language's ETag, so a
/// client that already holds this version gets a body-less 304.
/// </summary>
/// <remarks>
/// Sourced copy (WHO, Mayo Clinic, Cleveland Clinic, NHS, Egypt MOHP); a new
/// file with a new <c>version</c> reaches the app with no client change.
/// </remarks>
public sealed class PregnancyWeeksContent
{
    public const string ResourceName = "Balsm.API.Content.pregnancy-weeks.v1.json";
    public const int FirstWeek = 4;
    public const int LastWeek = 40;

    /// <summary>The <c>labels</c> the app draws the guide with; every one must be present.</summary>
    public static readonly IReadOnlyList<string> RequiredLabels =
    [
        "size_title", "size_about", "size_length", "size_weight", "unit_mm", "unit_cm", "unit_g", "unit_kg",
        "tests_due", "dangers_now", "dangers_all", "topics_title", "sources_title", "sources_more", "illustration",
    ];

    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,

        // Arabic as UTF-8 rather than \uXXXX escapes: an Arabic document is half the bytes. The body
        // is only ever sent as application/json, never embedded in HTML, so nothing needs escaping
        // beyond what JSON itself requires.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>English: the language a request gets unless it asks for Arabic.</summary>
    public const string English = "en";

    /// <summary>Arabic.</summary>
    public const string Arabic = "ar";

    /// <summary>Every language the content is served in.</summary>
    public static readonly IReadOnlyList<string> Languages = [English, Arabic];

    private static readonly Lazy<PregnancyWeeksContent> Shared = new(() => Load(
        typeof(PregnancyWeeksContent).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.")));

    private readonly IReadOnlyDictionary<string, LocalizedPregnancyWeeks> _byLanguage;

    private PregnancyWeeksContent(PregnancyWeeksFile file, IReadOnlyDictionary<string, LocalizedPregnancyWeeks> byLanguage)
    {
        File = file;
        _byLanguage = byLanguage;
    }

    public static PregnancyWeeksContent Current => Shared.Value;

    /// <summary>The content file as written, both languages side by side.</summary>
    public PregnancyWeeksFile File { get; }

    /// <summary>
    /// <paramref name="requested"/> as a language the content is served in: <c>ar</c> for Arabic,
    /// English for anything else, including none.
    /// </summary>
    public static string LanguageFor(string? requested) =>
        string.Equals(requested?.Trim(), Arabic, StringComparison.OrdinalIgnoreCase) ? Arabic : English;

    /// <summary>The document in <paramref name="requested"/> (see <see cref="LanguageFor"/>), ready to send.</summary>
    public LocalizedPregnancyWeeks For(string? requested) => _byLanguage[LanguageFor(requested)];

    public static PregnancyWeeksContent Load(Stream source)
    {
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        var raw = buffer.ToArray();

        var file = JsonSerializer.Deserialize<PregnancyWeeksFile>(raw, WireOptions)
            ?? throw new InvalidDataException("Pregnancy weeks content is empty.");
        Validate(file);

        var hash = Convert.ToHexStringLower(SHA256.HashData(raw))[..32];
        var byLanguage = Languages.ToDictionary(lang => lang, lang =>
        {
            var document = file.In(lang);
            var body = JsonSerializer.SerializeToUtf8Bytes(new PregnancyWeeksResponse(document), WireOptions);
            return new LocalizedPregnancyWeeks(document, body, new EntityTagHeaderValue($"\"pw-{lang}-{hash}\""));
        });
        return new PregnancyWeeksContent(file, byLanguage);
    }

    private static void Validate(PregnancyWeeksFile document)
    {
        if (string.IsNullOrWhiteSpace(document.Version))
            throw new InvalidDataException("Pregnancy weeks content has no version.");

        var expected = Enumerable.Range(FirstWeek, LastWeek - FirstWeek + 1);
        if (!document.Weeks.Select(w => w.Week).SequenceEqual(expected))
            throw new InvalidDataException($"Pregnancy weeks content must list weeks {FirstWeek}-{LastWeek} once each, ascending.");

        RequireDefined("source", document.Weeks.SelectMany(w => w.Sources), document.Sources.Select(s => s.Id));
        RequireDefined("danger sign", document.Weeks.SelectMany(w => w.DangerSigns), document.DangerSignLabels.Keys);
        RequireDefined("test", document.Weeks.SelectMany(w => w.TestsDue), document.TestLabels.Keys);
        RequireDefined("topic", document.Weeks.SelectMany(w => w.Topics), document.TopicLabels.Keys);
        RequireDefined("label", RequiredLabels, document.Labels.Keys);
        RequireDefined("illustration credit", document.Weeks.Where(w => w.Illustration is not null).Select(w => w.Illustration!.Credit), document.IllustrationCredits.Keys);
    }

    /// <summary>A key a week cites must be defined at the top, so a typo never reaches the app as a blank label or credit.</summary>
    private static void RequireDefined(string kind, IEnumerable<string> used, IEnumerable<string> defined)
    {
        var missing = used.Except(defined).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"Pregnancy weeks content cites undefined {kind} keys: {string.Join(", ", missing)}.");
    }
}

/// <summary>The guidance in one language: the document, its <c>{"data": …}</c> envelope as UTF-8, and its ETag.</summary>
/// <param name="Document">The document, every text in one language.</param>
/// <param name="Body">The envelope, ready to write.</param>
/// <param name="ETag">Strong ETag from the content file's bytes and the language, so each language revalidates on its own.</param>
public sealed record LocalizedPregnancyWeeks(PregnancyWeeksDto Document, byte[] Body, EntityTagHeaderValue ETag);
