using Balsm.Infrastructure.Maintenance;
using FluentAssertions;
using Xunit;

namespace Balsm.API.Tests.Maintenance;

public sealed class MaintenanceModeServiceTests : IDisposable
{
    private readonly MaintenanceHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task GetAsync_WhenNothingStored_ReturnsOff()
    {
        var state = await _h.Service.GetAsync();

        state.Should().Be(MaintenanceState.Default);
    }

    [Fact]
    public async Task SetAsync_WithReadonlyMessageAndEnd_StoresAndReturnsState()
    {
        var ends = MaintenanceHarness.Start.AddHours(2);

        var result = await _h.Service.SetAsync(new MaintenanceChange("ReadOnly", "  Back soon  ", ends), "admin:ops");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new MaintenanceState("readonly", "Back soon", ends, MaintenanceHarness.Start));
    }

    [Fact]
    public async Task SetAsync_ThenGetAfterCacheExpiry_ReadsPersistedState()
    {
        var ends = MaintenanceHarness.Start.AddHours(2);
        await _h.Service.SetAsync(new MaintenanceChange("block", null, ends), "admin:ops");
        var fresh = new MaintenanceHarnessReader(_h);

        var state = await fresh.ReadAsync();

        state.Mode.Should().Be("block");
        state.EndsAt.Should().Be(ends);
        state.Message.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_Off_ClearsMessageAndEnd()
    {
        await _h.Service.SetAsync(new MaintenanceChange("readonly", "msg", MaintenanceHarness.Start.AddHours(1)), "a");

        var result = await _h.Service.SetAsync(new MaintenanceChange("off", "ignored", MaintenanceHarness.Start.AddHours(1)), "a");

        result.Value.Should().Be(new MaintenanceState("off", null, null, MaintenanceHarness.Start));
    }

    [Fact]
    public async Task SetAsync_WritesAuditRowNamingActor()
    {
        await _h.Service.SetAsync(new MaintenanceChange("block", null, null), "admin:ops");

        var row = _h.Query(db => db.AuditLogs.Single(a => a.Action == "MaintenanceModeChanged"));

        row.Should().Match<Balsm.Infrastructure.Audit.AuditLog>(r =>
            r.Actor == "admin:ops" && r.Module == "Maintenance"
            && r.DetailsJson!.Contains("\"from\":\"off\"") && r.DetailsJson.Contains("\"to\":\"block\""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("maintenance")]
    public async Task SetAsync_WithUnknownMode_FailsValidation(string? mode)
    {
        var result = await _h.Service.SetAsync(new MaintenanceChange(mode, null, null), "a");

        result.Error.Should().Be(MaintenanceErrors.InvalidMode);
    }

    [Fact]
    public async Task SetAsync_WithTooLongMessage_FailsValidation()
    {
        var message = new string('x', MaintenanceLimits.MaxMessageLength + 1);

        var result = await _h.Service.SetAsync(new MaintenanceChange("readonly", message, null), "a");

        result.Error.Should().Be(MaintenanceErrors.MessageTooLong);
    }

    [Fact]
    public async Task SetAsync_WithEndInPast_FailsValidation()
    {
        var result = await _h.Service.SetAsync(
            new MaintenanceChange("readonly", null, MaintenanceHarness.Start.AddMinutes(-1)), "a");

        result.Error.Should().Be(MaintenanceErrors.EndsAtInPast);
    }

    [Fact]
    public async Task GetAsync_WithinCacheWindow_ServesSnapshotWithoutRereading()
    {
        await _h.Service.GetAsync();
        _h.Query(db =>
        {
            db.ServerConfigs.Add(new Balsm.Infrastructure.Platform.ServerConfigEntry
            {
                Key = "maintenance_mode",
                Value = "block",
            });
            return db.SaveChanges();
        });

        var cached = await _h.Service.GetAsync();
        _h.Clock.Now = _h.Clock.Now.AddSeconds(11);
        var reloaded = await _h.Service.GetAsync();

        (cached.Mode, reloaded.Mode).Should().Be(("off", "block"));
    }

    [Fact]
    public async Task GetAsync_WithConfigOverride_WinsOverStoredState()
    {
        using var h = new MaintenanceHarness(new MaintenanceOptions { Mode = "block", Message = "Upgrading" });

        var state = await h.Service.GetAsync();

        state.Should().Be(new MaintenanceState("block", "Upgrading", null, null));
    }

    [Fact]
    public async Task SetAsync_WithConfigOverride_ReturnsConflict()
    {
        using var h = new MaintenanceHarness(new MaintenanceOptions { Mode = "off" });

        var result = await h.Service.SetAsync(new MaintenanceChange("block", null, null), "a");

        result.Error.Should().Be(MaintenanceErrors.ConfigOverride);
    }

    /// <summary>Reads through the service after the snapshot window has passed.</summary>
    private sealed class MaintenanceHarnessReader(MaintenanceHarness h)
    {
        public ValueTask<MaintenanceState> ReadAsync()
        {
            h.Clock.Now = h.Clock.Now.AddMinutes(1);
            return h.Service.GetAsync();
        }
    }
}
