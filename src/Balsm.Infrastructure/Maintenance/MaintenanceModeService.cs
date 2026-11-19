using System.Globalization;
using System.Text.Json;
using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Infrastructure.Maintenance;

/// <summary>
/// Stores the maintenance state as three <see cref="ServerConfigEntry"/> rows in
/// the platform DB and keeps a short-lived in-process snapshot for reads.
/// Singleton: the DbContext is resolved per call from a fresh scope.
/// </summary>
public sealed class MaintenanceModeService(
    IServiceScopeFactory scopeFactory,
    IOptions<MaintenanceOptions> options,
    TimeProvider clock,
    ILogger<MaintenanceModeService> logger) : IMaintenanceModeService
{
    internal const string ModeKey = "maintenance_mode";
    internal const string MessageKey = "maintenance_message";
    internal const string EndsAtKey = "maintenance_ends_at";
    internal const string UpdatedAtKey = "maintenance_updated_at";

    private static readonly string[] Keys = [ModeKey, MessageKey, EndsAtKey, UpdatedAtKey];

    private volatile Snapshot? _snapshot;

    /// <inheritdoc />
    public async ValueTask<MaintenanceState> GetAsync(CancellationToken ct = default)
    {
        var pinned = FromConfiguration();
        if (pinned is not null)
            return pinned;

        var now = clock.GetUtcNow();
        var snap = _snapshot;
        if (snap is not null && snap.ExpiresAt > now)
            return snap.State;

        var state = await LoadAsync(ct).ConfigureAwait(false);
        _snapshot = new Snapshot(state, now.AddSeconds(Math.Max(0, options.Value.CacheSeconds)));
        return state;
    }

    /// <inheritdoc />
    public async Task<Result<MaintenanceState>> SetAsync(
        MaintenanceChange change, string actor, CancellationToken ct = default)
    {
        if (FromConfiguration() is not null)
            return Result.Failure<MaintenanceState>(MaintenanceErrors.ConfigOverride);

        var now = clock.GetUtcNow();
        var mode = MaintenanceModes.Normalize(change.Mode);
        if (mode is null)
            return Result.Failure<MaintenanceState>(MaintenanceErrors.InvalidMode);

        var message = string.IsNullOrWhiteSpace(change.Message) ? null : change.Message.Trim();
        if (message is { Length: > MaintenanceLimits.MaxMessageLength })
            return Result.Failure<MaintenanceState>(MaintenanceErrors.MessageTooLong);

        if (change.EndsAt is { } endsAt && endsAt <= now)
            return Result.Failure<MaintenanceState>(MaintenanceErrors.EndsAtInPast);

        // Turning maintenance off clears the message and end time so a later
        // switch-on never resurfaces stale copy.
        var next = mode == MaintenanceModes.Off
            ? new MaintenanceState(mode, null, null, now)
            : new MaintenanceState(mode, message, change.EndsAt?.ToUniversalTime(), now);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var previous = await ReadAsync(db, ct).ConfigureAwait(false);

        var rows = await db.ServerConfigs
            .Where(c => Keys.Contains(c.Key))
            .ToDictionaryAsync(c => c.Key, ct)
            .ConfigureAwait(false);
        Upsert(db, rows, ModeKey, next.Mode);
        Upsert(db, rows, MessageKey, next.Message ?? "");
        Upsert(db, rows, EndsAtKey, next.EndsAt?.ToString("O", CultureInfo.InvariantCulture) ?? "");
        Upsert(db, rows, UpdatedAtKey, now.ToString("O", CultureInfo.InvariantCulture));
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var audit = scope.ServiceProvider.GetRequiredService<IAuditLogWriter>();
        var ctx = AuditContext.Current;
        await audit.WriteAsync(new AuditLog
        {
            OccurredAt = now.UtcDateTime,
            Actor = actor,
            SourceIp = ctx?.SourceIp,
            CorrelationId = ctx?.CorrelationId,
            Module = "Maintenance",
            Action = "MaintenanceModeChanged",
            TargetType = "MaintenanceMode",
            DetailsJson = JsonSerializer.Serialize(new
            {
                from = previous.Mode,
                to = next.Mode,
                ends_at = next.EndsAt,
                has_message = next.Message is not null,
            }),
        }, ct).ConfigureAwait(false);

        _snapshot = new Snapshot(next, now.AddSeconds(Math.Max(0, options.Value.CacheSeconds)));
        logger.LogInformation(
            "Maintenance mode changed {From} -> {To} by {Actor} (Module={Module}, Action={Action})",
            previous.Mode, next.Mode, actor, "Maintenance", "MaintenanceModeChanged");

        return Result.Success(next);
    }

    private MaintenanceState? FromConfiguration()
    {
        var o = options.Value;
        var mode = MaintenanceModes.Normalize(o.Mode);
        if (mode is null)
            return null;
        return mode == MaintenanceModes.Off
            ? MaintenanceState.Default
            : new MaintenanceState(mode, string.IsNullOrWhiteSpace(o.Message) ? null : o.Message.Trim(), o.EndsAt, null);
    }

    private async Task<MaintenanceState> LoadAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await ReadAsync(db, ct).ConfigureAwait(false);
    }

    private static async Task<MaintenanceState> ReadAsync(PlatformDbContext db, CancellationToken ct)
    {
        var values = await db.ServerConfigs
            .AsNoTracking()
            .Where(c => Keys.Contains(c.Key))
            .Select(c => new { c.Key, c.Value })
            .ToDictionaryAsync(c => c.Key, c => c.Value, ct)
            .ConfigureAwait(false);

        var mode = MaintenanceModes.Normalize(values.GetValueOrDefault(ModeKey)) ?? MaintenanceModes.Off;
        var message = values.GetValueOrDefault(MessageKey);
        return new MaintenanceState(
            mode,
            string.IsNullOrWhiteSpace(message) ? null : message,
            ParseInstant(values.GetValueOrDefault(EndsAtKey)),
            ParseInstant(values.GetValueOrDefault(UpdatedAtKey)));
    }

    private static DateTimeOffset? ParseInstant(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var v)
            ? v
            : null;

    private static void Upsert(
        PlatformDbContext db, Dictionary<string, ServerConfigEntry> rows, string key, string value)
    {
        if (rows.TryGetValue(key, out var row))
        {
            row.Value = value;
            return;
        }

        db.ServerConfigs.Add(new ServerConfigEntry { Key = key, Value = value });
    }

    private sealed record Snapshot(MaintenanceState State, DateTimeOffset ExpiresAt);
}
