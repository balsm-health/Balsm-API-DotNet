using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace Balsm.API.Content;

/// <summary>
/// The week-by-week pregnancy guidance shipped inside the API assembly as
/// <c>Content/pregnancy-weeks.v1.json</c>. Read, validated and serialised once;
/// every request is served the same bytes and the same ETag, so a client that
/// already holds this version gets a body-less 304.
/// </summary>
/// <remarks>
/// Placeholder copy for now. Partner content replaces the file — same shape,
/// new <c>version</c> — and reaches the app with no client change.
/// </remarks>
public sealed class PregnancyWeeksContent
{
    public const string ResourceName = "Balsm.API.Content.pregnancy-weeks.v1.json";
    public const int FirstWeek = 4;
    public const int LastWeek = 40;

    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly Lazy<PregnancyWeeksContent> Shared = new(() => Load(
        typeof(PregnancyWeeksContent).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.")));

    private PregnancyWeeksContent(PregnancyWeeksDto document, byte[] body, EntityTagHeaderValue etag)
    {
        Document = document;
        Body = body;
        ETag = etag;
    }

    public static PregnancyWeeksContent Current => Shared.Value;

    /// <summary>The parsed document.</summary>
    public PregnancyWeeksDto Document { get; }

    /// <summary>The <c>{"data": …}</c> envelope, UTF-8, ready to write.</summary>
    public byte[] Body { get; }

    /// <summary>Strong ETag derived from the content file's bytes.</summary>
    public EntityTagHeaderValue ETag { get; }

    public static PregnancyWeeksContent Load(Stream source)
    {
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        var raw = buffer.ToArray();

        var document = JsonSerializer.Deserialize<PregnancyWeeksDto>(raw, WireOptions)
            ?? throw new InvalidDataException("Pregnancy weeks content is empty.");
        Validate(document);

        var body = JsonSerializer.SerializeToUtf8Bytes(new PregnancyWeeksResponse(document), WireOptions);
        var hash = Convert.ToHexStringLower(SHA256.HashData(raw))[..32];
        return new PregnancyWeeksContent(document, body, new EntityTagHeaderValue($"\"pw-{hash}\""));
    }

    private static void Validate(PregnancyWeeksDto document)
    {
        if (string.IsNullOrWhiteSpace(document.Version))
            throw new InvalidDataException("Pregnancy weeks content has no version.");

        var expected = Enumerable.Range(FirstWeek, LastWeek - FirstWeek + 1);
        if (!document.Weeks.Select(w => w.Week).SequenceEqual(expected))
            throw new InvalidDataException($"Pregnancy weeks content must list weeks {FirstWeek}-{LastWeek} once each, ascending.");
    }
}
