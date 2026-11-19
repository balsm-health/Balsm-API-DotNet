using Balsm.Infrastructure.Maintenance;
using Balsm.SharedKernel.Results;
using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Balsm.Supervisor.Tests.Controllers;

public sealed class AdminMaintenanceControllerTests
{
    private static readonly MaintenanceState Block = new("block", null, null, null);

    [Fact]
    public async Task Put_WithAdminSession_PassesSessionUserAsActor()
    {
        var fake = new FakeMaintenance(Result.Success(Block));
        var sessions = new AdminSessionService();
        var token = sessions.CreateSession("ops");
        var controller = Create(fake, sessions, $"balsm_admin_session={Uri.EscapeDataString(token)}");

        var result = await controller.Put(new UpdateMaintenanceModeRequest("block", null, null), CancellationToken.None);

        (result.Result is OkObjectResult, fake.LastActor).Should().Be((true, "admin:ops"));
    }

    [Fact]
    public async Task Put_WhenValidationFails_Returns400WithCode()
    {
        var controller = Create(new FakeMaintenance(Result.Failure<MaintenanceState>(MaintenanceErrors.InvalidMode)));

        var result = await controller.Put(new UpdateMaintenanceModeRequest("nope", null, null), CancellationToken.None);

        var obj = result.Result.Should().BeOfType<ObjectResult>().Subject;
        (obj.StatusCode, ((ProblemDetails)obj.Value!).Extensions["code"])
            .Should().Be((400, (object?)"maintenance.validation"));
    }

    [Fact]
    public async Task Put_WhenPinnedByConfiguration_Returns409()
    {
        var controller = Create(new FakeMaintenance(Result.Failure<MaintenanceState>(MaintenanceErrors.ConfigOverride)));

        var result = await controller.Put(new UpdateMaintenanceModeRequest("off", null, null), CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Get_ReturnsCurrentState()
    {
        var controller = Create(new FakeMaintenance(Result.Success(Block)));

        var result = await controller.Get(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(new MaintenanceStatusResponse(Block));
    }

    private static AdminMaintenanceController Create(
        FakeMaintenance fake, AdminSessionService? sessions = null, string? cookie = null)
    {
        var context = new DefaultHttpContext();
        if (cookie is not null)
            context.Request.Headers.Cookie = cookie;
        return new AdminMaintenanceController(fake, sessions ?? new AdminSessionService())
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };
    }

    private sealed class FakeMaintenance(Result<MaintenanceState> setResult) : IMaintenanceModeService
    {
        public string? LastActor { get; private set; }

        public ValueTask<MaintenanceState> GetAsync(CancellationToken ct = default) => ValueTask.FromResult(Block);

        public Task<Result<MaintenanceState>> SetAsync(MaintenanceChange change, string actor, CancellationToken ct = default)
        {
            LastActor = actor;
            return Task.FromResult(setResult);
        }
    }
}
