using Balsm.API.Controllers;
using Balsm.API.Tests.Platform;
using Balsm.Infrastructure.Backup;
using Balsm.Infrastructure.Configuration;
using Balsm.Infrastructure.Lifecycle;
using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Balsm.API.Tests.Controllers;

public sealed class BackupsControllerTests : IDisposable
{
    private static readonly DateTime Base = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly PlatformDbHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private sealed class StubBackupService(Result<BackupFile> result) : IBackupService
    {
        public Task<Result<BackupFile>> BackupNowAsync(BackupTrigger trigger, CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    [Fact]
    public async Task List_ReturnsLiveBackupsNewestFirst()
    {
        _harness.Db.BackupFiles.AddRange(
            Backup("older.db"),
            Backup("newer.db"),
            Backup("gone.db", deleted: true));
        await _harness.Db.SaveChangesAsync();
        await StampCreatedAtAsync("older.db", Base);
        await StampCreatedAtAsync("newer.db", Base.AddDays(1));

        var body = ActionResultReader.Ok(await Controller().List());

        body.GetProperty("items")[0].GetProperty("Filename").GetString().Should().Be("newer.db");
    }

    [Fact]
    public async Task List_OutOfRangePaging_FallsBackToDefaults()
    {
        var body = ActionResultReader.Ok(await Controller().List(page: 0, pageSize: 1000));

        (body.GetProperty("page").GetInt32(), body.GetProperty("pageSize").GetInt32()).Should().Be((1, 20));
    }

    [Fact]
    public async Task List_ZeroPageSize_FallsBackToTwenty()
    {
        var body = ActionResultReader.Ok(await Controller().List(pageSize: 0));

        body.GetProperty("pageSize").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task TriggerBackup_ServiceSucceeds_ReturnsTheBackup()
    {
        var controller = Controller(Result.Success(Backup("manual.db")));

        var body = ActionResultReader.Ok(await controller.TriggerBackup(default));

        body.GetProperty("Filename").GetString().Should().Be("manual.db");
    }

    [Fact]
    public async Task TriggerBackup_ServiceFails_Returns400WithTheReason()
    {
        var controller = Controller(Result.Failure<BackupFile>(new Error("Backup.Failed", "disk full")));

        var body = ActionResultReader.Body<BadRequestObjectResult>(await controller.TriggerBackup(default));

        body.GetProperty("error").GetString().Should().Be("disk full");
    }

    [Fact]
    public async Task GetSchedule_SeededDefaults_ReturnsThemWithTheResolvedDirectory()
    {
        var body = ActionResultReader.Ok(await Controller().GetSchedule(default));

        body.GetProperty("directory").GetString().Should().Be(Path.GetFullPath("backups"));
    }

    [Fact]
    public async Task GetSchedule_NoConfigRows_FallsBackToDefaults()
    {
        _harness.Db.ServerConfigs.RemoveRange(_harness.Db.ServerConfigs);
        await _harness.Db.SaveChangesAsync();

        var body = ActionResultReader.Ok(await Controller().GetSchedule(default));

        (body.GetProperty("cron").GetString(), body.GetProperty("retention").GetInt32())
            .Should().Be(("0 2 * * *", 30));
    }

    [Fact]
    public async Task UpdateSchedule_ValidRequest_PersistsBothValues()
    {
        await Controller().UpdateSchedule(new UpdateScheduleRequest("30 1 * * *", 7), default);

        var body = ActionResultReader.Ok(await Controller().GetSchedule(default));
        (body.GetProperty("cron").GetString(), body.GetProperty("retention").GetInt32())
            .Should().Be(("30 1 * * *", 7));
    }

    [Fact]
    public async Task UpdateSchedule_NoExistingRows_InsertsThem()
    {
        _harness.Db.ServerConfigs.RemoveRange(_harness.Db.ServerConfigs);
        await _harness.Db.SaveChangesAsync();

        await Controller().UpdateSchedule(new UpdateScheduleRequest("0 1 * * *", 3), default);

        (await _harness.NewContext().ServerConfigs.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("every day")]
    public async Task UpdateSchedule_InvalidCron_Returns400(string cron)
    {
        var result = await Controller().UpdateSchedule(new UpdateScheduleRequest(cron, 7), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Restore_WrongConfirmPhrase_Returns400()
    {
        var result = await Controller().Restore(Guid.NewGuid(), new RestoreRequest("restore"), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Restore_UnknownBackup_Returns400WithTheReason()
    {
        var result = await Controller().Restore(Guid.NewGuid(), new RestoreRequest("RESTORE"), default);

        ActionResultReader.Body<BadRequestObjectResult>(result)
            .GetProperty("error").GetString().Should().Be("Backup file not found.");
    }

    [Fact]
    public async Task Restore_ValidBackup_Returns202()
    {
        var dir = Directory.CreateTempSubdirectory("balsm-restore-ctrl-");
        try
        {
            var live = Path.Combine(dir.FullName, "live.db");
            var source = Path.Combine(dir.FullName, "source.db");
            SqliteFiles.Create(live);
            SqliteFiles.Create(source);
            var backup = Backup("source.db", path: source, sha: SqliteFiles.Sha256(source));
            _harness.Db.BackupFiles.Add(backup);
            await _harness.Db.SaveChangesAsync();
            var controller = Controller(connectionString: $"Data Source={live}");

            var result = await controller.Restore(backup.Id, new RestoreRequest("RESTORE"), default);

            result.Should().BeOfType<AcceptedResult>();
        }
        finally
        {
            SqliteFiles.DeleteDirectory(dir.FullName);
        }
    }

    private BackupsController Controller(Result<BackupFile>? backupResult = null, string connectionString = "Data Source=unused.db")
    {
        var db = _harness.NewContext();
        var restore = new RestoreOrchestrator(
            db,
            Options.Create(new DatabaseOptions { ConnectionString = connectionString }),
            new ReadinessGate(),
            NullLogger<RestoreOrchestrator>.Instance);
        return new BackupsController(
            new StubBackupService(backupResult ?? Result.Failure<BackupFile>(Error.NotFound)),
            restore,
            db,
            Options.Create(new BackupOptions()));
    }

    private async Task StampCreatedAtAsync(string filename, DateTime at)
    {
        // SaveChanges stamps CreatedAt with the clock; pin it so the order is deterministic.
        await _harness.Db.Database.ExecuteSqlAsync(
            $"UPDATE BackupFiles SET CreatedAt = {at} WHERE Filename = {filename}");
    }

    private static BackupFile Backup(string filename, bool deleted = false, string? path = null, string sha = "00") =>
        new()
        {
            Filename = filename,
            Path = path ?? filename,
            Sha256 = sha,
            Status = BackupStatus.Completed,
            IsDeleted = deleted,
        };
}
