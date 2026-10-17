using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Balsm.API.Controllers;

[ApiController]
[Route("api/v1/admin/audit")]
public class AuditController : ControllerBase
{
    private readonly PlatformDbContext _db;

    public AuditController(PlatformDbContext db)
    {
        _db = db;
    }

    /// <summary>GET /api/v1/admin/audit/logs — paged + filtered audit log</summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? module = null,
        [FromQuery] string? action = null,
        [FromQuery] string? actor = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 50;

        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(l => !l.IsDeleted);

        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(l => l.Module == module);
        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action == action);
        if (!string.IsNullOrWhiteSpace(actor))
            query = query.Where(l => l.Actor == actor);
        if (from.HasValue)
            query = query.Where(l => l.OccurredAt >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.OccurredAt <= to.Value);

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(l => l.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.OccurredAt,
                l.Actor,
                l.SourceIp,
                l.Module,
                l.Action,
                l.TargetType,
                l.TargetId,
                l.DetailsJson,
                l.CorrelationId
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>GET /api/v1/admin/audit/retention</summary>
    [HttpGet("retention")]
    public async Task<IActionResult> GetRetention(CancellationToken ct)
    {
        var cronEntry = await _db.ServerConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "audit_retention_cron", ct)
            .ConfigureAwait(false);
        var yearsEntry = await _db.ServerConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "audit_retention_years", ct)
            .ConfigureAwait(false);

        return Ok(new
        {
            cron = cronEntry?.Value ?? "0 3 * * *",
            retentionYears = int.TryParse(yearsEntry?.Value, out var y) ? y : 2
        });
    }

    /// <summary>PUT /api/v1/admin/audit/retention</summary>
    [HttpPut("retention")]
    public async Task<IActionResult> UpdateRetention(
        [FromBody] UpdateAuditRetentionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Cron))
            return BadRequest(new { error = "cron is required" });

        if (NCrontab.CrontabSchedule.TryParse(request.Cron) is null)
            return BadRequest(new { error = "Invalid cron expression" });

        if (request.RetentionYears < 1)
            return BadRequest(new { error = "retentionYears must be >= 1" });

        await UpsertConfigAsync("audit_retention_cron", request.Cron, ct).ConfigureAwait(false);
        await UpsertConfigAsync("audit_retention_years", request.RetentionYears.ToString(), ct)
            .ConfigureAwait(false);

        return Ok(new { cron = request.Cron, retentionYears = request.RetentionYears });
    }

    /// <summary>GET /api/v1/admin/audit/archives — paged list of audit archives</summary>
    [HttpGet("archives")]
    public async Task<IActionResult> GetArchives(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var total = await _db.AuditArchives
            .CountAsync(a => !a.IsDeleted, ct)
            .ConfigureAwait(false);

        var items = await _db.AuditArchives
            .AsNoTracking()
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.ArchivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.Filename,
                a.SizeBytes,
                a.Sha256,
                a.ArchivedAt,
                a.PeriodStart,
                a.PeriodEnd,
                a.RowCount
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return Ok(new { total, page, pageSize, items });
    }

    private async Task UpsertConfigAsync(string key, string value, CancellationToken ct)
    {
        var entry = await _db.ServerConfigs.FirstOrDefaultAsync(c => c.Key == key, ct)
            .ConfigureAwait(false);
        if (entry is null)
        {
            _db.ServerConfigs.Add(new ServerConfigEntry { Key = key, Value = value });
        }
        else
        {
            entry.Value = value;
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}

public sealed record UpdateAuditRetentionRequest(string Cron, int RetentionYears);
