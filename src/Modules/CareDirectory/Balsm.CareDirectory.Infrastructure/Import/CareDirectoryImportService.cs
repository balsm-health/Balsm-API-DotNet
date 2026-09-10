using Balsm.CareDirectory.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.CareDirectory.Infrastructure.Import;

/// <summary>
/// Imports the care-directory artifact on startup, after migrations.
///
/// A hosted service rather than a CLI because the directory ships inside a
/// self-hosted deployment: an operator who never ran the import command would be
/// left with a blank map and no obvious cause. MigrationRunner already
/// auto-migrates on boot; this follows the same pattern and is registered after
/// it, so the schema exists by the time it runs.
/// </summary>
public sealed class CareDirectoryImportService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    IOptions<CareDirectoryOptions> options,
    ILogger<CareDirectoryImportService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.ImportOnStartup)
        {
            logger.LogInformation("Care directory import disabled by configuration");
            return;
        }

        var path = ResolveArtifact(settings.ArtifactPath);
        if (path is null)
        {
            // A missing artifact must not take the host down: every other module
            // still works, and the map degrades to empty rather than the server
            // refusing to start.
            logger.LogWarning(
                "Care directory artifact {Artifact} not found under {Base} or {ContentRoot} — directory will be empty",
                settings.ArtifactPath, AppContext.BaseDirectory, environment.ContentRootPath);
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CareDirectoryDbContext>();
            var importer = new CareDirectoryImporter(db, settings);

            await using var artifact = CareDirectoryImporter.Open(path);
            var result = await importer.ImportAsync(artifact, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Care directory import complete — {Inserted} inserted, {Updated} updated, {Skipped} below confidence floor",
                result.Inserted, result.Updated, result.Skipped);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Care directory import failed — directory may be stale or empty");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // The artifact is copied to the BUILD OUTPUT directory, which is where a
    // published deployment runs from. Under `dotnet run` the content root is the
    // project directory instead, so probe both rather than only one — resolving
    // from the content root alone silently finds nothing in development.
    private string? ResolveArtifact(string relativePath)
    {
        foreach (var root in new[] { AppContext.BaseDirectory, environment.ContentRootPath })
        {
            var candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
