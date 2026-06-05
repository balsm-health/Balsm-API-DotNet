using Balsm.Infrastructure.Backup;
using Balsm.Infrastructure.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Balsm.API.Controllers;

[ApiController]
[Route("api/v1/admin/backups")]
public class BackupsController : ControllerBase
{
    private readonly IBackupService _backupService;
    private readonly RestoreOrchestrator _restoreOrchestrator;
    private readonly PlatformDbContext _db;

    public BackupsController(
        IBackupService backupService,
        RestoreOrchestrator restoreOrchestrator,
        PlatformDbContext db)
    {
        _backupService = backupService;
        _restoreOrchestrator = restoreOrchestrator;
        _db = db;
    }

    /// <summary>GET /api/v1/admin/backups — paged list of backups</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var total = await _db.BackupFiles
            .CountAsync(b => !b.IsDeleted, ct)
            .ConfigureAwait(false);

        var items = await _db.BackupFiles
            .Where(b => !b.IsDeleted)
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.Filename,
                b.SizeBytes,
                b.Sha256,
                b.Trigger,
                b.Status,
                b.CreatedAt
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>POST /api/v1/admin/backups — trigger on-demand backup</summary>
    [HttpPost]
    public async Task<IActionResult> TriggerBackup(CancellationToken ct)
    {
        var result = await _backupService.BackupNowAsync(BackupTrigger.Manual, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error?.Message });

        return Ok(new
        {
            result.Value!.Id,
            result.Value.Filename,
            result.Value.SizeBytes,
            result.Value.Sha256,
            result.Value.Status,
            result.Value.CreatedAt
        });
    }

    /// <summary>GET /api/v1/admin/backups/schedule</summary>
    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule(CancellationToken ct)
    {
        var cronEntry = await _db.ServerConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "backup_cron", ct)
            .ConfigureAwait(false);
        var retentionEntry = await _db.ServerConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == "backup_retention", ct)
            .ConfigureAwait(false);

        return Ok(new
        {
            cron = cronEntry?.Value ?? "0 2 * * *",
            retention = int.TryParse(retentionEntry?.Value, out var r) ? r : 30
        });
    }

    /// <summary>PUT /api/v1/admin/backups/schedule</summary>
    [HttpPut("schedule")]
    public async Task<IActionResult> UpdateSchedule(
        [FromBody] UpdateScheduleRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Cron))
            return BadRequest(new { error = "cron is required" });

        if (NCrontab.CrontabSchedule.TryParse(request.Cron) is null)
            return BadRequest(new { error = "Invalid cron expression" });

        await UpsertConfigAsync("backup_cron", request.Cron, ct).ConfigureAwait(false);
        await UpsertConfigAsync("backup_retention", request.Retention.ToString(), ct).ConfigureAwait(false);

        return Ok(new { cron = request.Cron, retention = request.Retention });
    }

    /// <summary>POST /api/v1/admin/backups/{id}/restore</summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(
        Guid id,
        [FromBody] RestoreRequest request,
        CancellationToken ct)
    {
        if (request.ConfirmPhrase != "RESTORE")
            return BadRequest(new { error = "confirm_phrase must be 'RESTORE'" });

        var result = await _restoreOrchestrator.RestoreAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error?.Message });

        return Accepted(new { message = "Restore initiated. Service will restart shortly." });
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

public sealed record UpdateScheduleRequest(string Cron, int Retention);
public sealed record RestoreRequest(string ConfirmPhrase);
