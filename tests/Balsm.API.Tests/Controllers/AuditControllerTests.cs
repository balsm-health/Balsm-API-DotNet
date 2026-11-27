using Balsm.API.Controllers;
using Balsm.API.Tests.Platform;
using Balsm.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.API.Tests.Controllers;

public sealed class AuditControllerTests : IDisposable
{
    private static readonly DateTime Base = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly PlatformDbHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task GetLogs_NoFilters_ReturnsLiveRowsNewestFirst()
    {
        await SeedLogsAsync();

        var body = ActionResultReader.Ok(await Controller().GetLogs());

        body.GetProperty("total").GetInt32().Should().Be(3);
        body.GetProperty("items")[0].GetProperty("Action").GetString().Should().Be("Deleted");
    }

    [Fact]
    public async Task GetLogs_EveryFilter_NarrowsToTheMatchingRow()
    {
        await SeedLogsAsync();

        var body = ActionResultReader.Ok(await Controller().GetLogs(
            module: "Account", action: "Updated", actor: "admin@example.test",
            from: Base.AddHours(1), to: Base.AddHours(1)));

        body.GetProperty("total").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetLogs_OutOfRangePaging_FallsBackToDefaults()
    {
        var body = ActionResultReader.Ok(await Controller().GetLogs(page: 0, pageSize: 500));

        (body.GetProperty("page").GetInt32(), body.GetProperty("pageSize").GetInt32()).Should().Be((1, 50));
    }

    [Fact]
    public async Task GetLogs_ZeroPageSize_FallsBackToFifty()
    {
        var body = ActionResultReader.Ok(await Controller().GetLogs(pageSize: 0));

        body.GetProperty("pageSize").GetInt32().Should().Be(50);
    }

    [Fact]
    public async Task GetRetention_SeededDefaults_ReturnsThem()
    {
        var body = ActionResultReader.Ok(await Controller().GetRetention(default));

        (body.GetProperty("cron").GetString(), body.GetProperty("retentionYears").GetInt32())
            .Should().Be(("0 3 * * *", 2));
    }

    [Fact]
    public async Task GetRetention_NoConfigRows_FallsBackToDefaults()
    {
        _harness.Db.ServerConfigs.RemoveRange(_harness.Db.ServerConfigs);
        await _harness.Db.SaveChangesAsync();

        var body = ActionResultReader.Ok(await Controller().GetRetention(default));

        (body.GetProperty("cron").GetString(), body.GetProperty("retentionYears").GetInt32())
            .Should().Be(("0 3 * * *", 2));
    }

    [Fact]
    public async Task UpdateRetention_ValidRequest_PersistsBothValues()
    {
        await Controller().UpdateRetention(new UpdateAuditRetentionRequest("0 4 * * *", 5), default);

        var body = ActionResultReader.Ok(await Controller().GetRetention(default));
        (body.GetProperty("cron").GetString(), body.GetProperty("retentionYears").GetInt32())
            .Should().Be(("0 4 * * *", 5));
    }

    [Fact]
    public async Task UpdateRetention_NoExistingRows_InsertsThem()
    {
        _harness.Db.ServerConfigs.RemoveRange(_harness.Db.ServerConfigs);
        await _harness.Db.SaveChangesAsync();

        await Controller().UpdateRetention(new UpdateAuditRetentionRequest("0 5 * * *", 3), default);

        (await _harness.NewContext().ServerConfigs.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData("", 2)]
    [InlineData("not a cron", 2)]
    [InlineData("0 3 * * *", 0)]
    public async Task UpdateRetention_InvalidRequest_Returns400(string cron, int years)
    {
        var result = await Controller().UpdateRetention(new UpdateAuditRetentionRequest(cron, years), default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetArchives_ReturnsLiveArchivesNewestFirst()
    {
        _harness.Db.AuditArchives.AddRange(
            Archive("older.jsonl", Base),
            Archive("newer.jsonl", Base.AddDays(1)),
            Archive("gone.jsonl", Base.AddDays(2), deleted: true));
        await _harness.Db.SaveChangesAsync();

        var body = ActionResultReader.Ok(await Controller().GetArchives());

        body.GetProperty("items")[0].GetProperty("Filename").GetString().Should().Be("newer.jsonl");
    }

    [Fact]
    public async Task GetArchives_OutOfRangePaging_FallsBackToDefaults()
    {
        var body = ActionResultReader.Ok(await Controller().GetArchives(page: -1, pageSize: 101));

        (body.GetProperty("page").GetInt32(), body.GetProperty("pageSize").GetInt32()).Should().Be((1, 20));
    }

    [Fact]
    public async Task GetArchives_ZeroPageSize_FallsBackToTwenty()
    {
        var body = ActionResultReader.Ok(await Controller().GetArchives(pageSize: 0));

        body.GetProperty("pageSize").GetInt32().Should().Be(20);
    }

    private AuditController Controller() => new(_harness.NewContext());

    private async Task SeedLogsAsync()
    {
        _harness.Db.AuditLogs.AddRange(
            Log(1, "Account", "Created", "system", Base),
            Log(2, "Account", "Updated", "admin@example.test", Base.AddHours(1)),
            Log(3, "Auth", "Deleted", "system", Base.AddHours(2)),
            Log(4, "Auth", "Archived", "system", Base.AddHours(3), deleted: true));
        await _harness.Db.SaveChangesAsync();
    }

    // Sequence is set explicitly: SQLite has no generator for it (see the report on AuditLog.Sequence).
    private static AuditLog Log(long sequence, string module, string action, string actor, DateTime at, bool deleted = false) =>
        new()
        {
            Sequence = sequence,
            Module = module,
            Action = action,
            Actor = actor,
            OccurredAt = at,
            IsDeleted = deleted,
        };

    private static AuditArchive Archive(string filename, DateTime at, bool deleted = false) =>
        new()
        {
            Filename = filename,
            Path = filename,
            ArchivedAt = at,
            PeriodStart = at,
            PeriodEnd = at,
            IsDeleted = deleted,
        };
}
