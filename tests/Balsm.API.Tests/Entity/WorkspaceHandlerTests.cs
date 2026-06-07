using Balsm.Entity.Application.Commands;
using Balsm.Entity.Application.Handlers;
using Balsm.Entity.Application.Queries;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.API.Tests.Entity;

public class WorkspaceHandlerTests
{
    private static CreateWorkspaceCommandHandler CreateHandler(EntityModuleHarness h)
        => new(h.WorkspaceRepo, h.Uow, NullLogger<CreateWorkspaceCommandHandler>.Instance);

    [Fact]
    public async Task CreateWorkspace_FirstTime_SucceedsAndPersists()
    {
        using var h = new EntityModuleHarness();
        var handler = CreateHandler(h);

        var result = await handler.Handle(new CreateWorkspaceCommand("Cairo Pharmacy", "Cairo-Pharmacy", "ar"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Cairo Pharmacy");
        result.Value.Slug.Should().Be("cairo-pharmacy"); // lower-cased by domain
        result.Value.LocaleDefault.Should().Be("ar");

        await using var verify = h.NewContext();
        (await verify.Workspaces.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateWorkspace_SecondTime_FailsWithAlreadyExists()
    {
        using var h = new EntityModuleHarness();
        var handler = CreateHandler(h);

        var first = await handler.Handle(new CreateWorkspaceCommand("First", "first"), default);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.Handle(new CreateWorkspaceCommand("Second", "second"), default);

        second.IsFailure.Should().BeTrue();
        second.Error!.Code.Should().Be("Workspace.AlreadyExists");

        // One-workspace-per-server constraint: still exactly one row, unchanged.
        await using var verify = h.NewContext();
        var rows = await verify.Workspaces.ToListAsync();
        rows.Should().ContainSingle().Which.Name.Should().Be("First");
    }

    [Fact]
    public async Task GetWorkspace_WhenNoneExists_ReturnsNotFound()
    {
        using var h = new EntityModuleHarness();
        var handler = new GetWorkspaceQueryHandler(h.WorkspaceRepo);

        var result = await handler.Handle(new GetWorkspaceQuery(), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task GetWorkspace_AfterCreate_ReturnsWorkspace()
    {
        using var h = new EntityModuleHarness();
        await CreateHandler(h).Handle(new CreateWorkspaceCommand("Main", "main"), default);

        var result = await new GetWorkspaceQueryHandler(h.WorkspaceRepo).Handle(new GetWorkspaceQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Main");
        result.Value.Status.Should().Be("Active");
    }

    [Fact]
    public async Task UpdateWorkspace_RenamesAndUpdatesLocale()
    {
        using var h = new EntityModuleHarness();
        var created = await CreateHandler(h).Handle(new CreateWorkspaceCommand("Old Name", "old", "en"), default);

        var update = await new UpdateWorkspaceCommandHandler(h.WorkspaceRepo, h.Uow)
            .Handle(new UpdateWorkspaceCommand(created.Value!.Id, "New Name", "ar"), default);

        update.IsSuccess.Should().BeTrue();
        update.Value!.Name.Should().Be("New Name");
        update.Value.LocaleDefault.Should().Be("ar");

        await using var verify = h.NewContext();
        var ws = await verify.Workspaces.SingleAsync();
        ws.Name.Should().Be("New Name");
        ws.LocaleDefault.Should().Be("ar");
    }

    [Fact]
    public async Task UpdateWorkspace_UnknownId_ReturnsNotFound()
    {
        using var h = new EntityModuleHarness();

        var result = await new UpdateWorkspaceCommandHandler(h.WorkspaceRepo, h.Uow)
            .Handle(new UpdateWorkspaceCommand(Guid.NewGuid(), "X", "en"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NotFound");
    }
}
