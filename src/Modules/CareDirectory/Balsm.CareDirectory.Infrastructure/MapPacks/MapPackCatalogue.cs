using System.Text.Json;
using System.Text.Json.Serialization;
using Balsm.CareDirectory.Application.Queries;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>
/// Reads the map-pack catalogue that <c>tools/map-packs/publish.py</c> produces.
///
/// A committed artifact rather than a database table, matching the care-directory
/// import beside it: the catalogue changes only when a pack set is published, it
/// is small, and keeping it in the repo means the exact bytes a deployment serves
/// stay auditable.
///
/// Loaded once and held: 27 entries that cannot change without a deployment.
/// </summary>
public sealed class MapPackCatalogue(IHostEnvironment environment, ILogger<MapPackCatalogue> logger)
{
    private const string RelativePath = "data/map-packs/manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private IReadOnlyList<MapPackDto>? _packs;

    public IReadOnlyList<MapPackDto> Packs => _packs ??= Load();

    private IReadOnlyList<MapPackDto> Load()
    {
        var path = Resolve();
        if (path is null)
        {
            // A missing catalogue must not take the host down, for the same
            // reason a missing directory artifact does not: the rest of the API
            // works and the app simply offers no packs to download.
            logger.LogWarning(
                "Map-pack catalogue {Path} not found under {Base} or {ContentRoot} — no packs will be offered",
                RelativePath, AppContext.BaseDirectory, environment.ContentRootPath);
            return [];
        }

        try
        {
            var doc = JsonSerializer.Deserialize<ManifestFile>(File.ReadAllText(path), JsonOptions);
            var packs = doc?.Packs ?? [];
            logger.LogInformation("Map-pack catalogue loaded — {Count} packs", packs.Count);
            return packs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map-pack catalogue at {Path} could not be read — no packs will be offered", path);
            return [];
        }
    }

    // Copied to the BUILD OUTPUT for a published deployment, but the content
    // root is the project directory under `dotnet run` — probe both, or
    // development silently finds nothing.
    private string? Resolve()
    {
        foreach (var root in new[] { AppContext.BaseDirectory, environment.ContentRootPath })
        {
            var candidate = Path.Combine(root, RelativePath);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private sealed record ManifestFile([property: JsonPropertyName("packs")] List<MapPackDto> Packs);
}
