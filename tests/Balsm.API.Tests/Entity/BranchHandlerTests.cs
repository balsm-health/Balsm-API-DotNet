using Balsm.Entity.Application.Commands;
using Balsm.Entity.Application.Handlers;
using Balsm.Entity.Application.Queries;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.API.Tests.Entity;

public class BranchHandlerTests
{
    private static readonly Guid Workspace = Guid.NewGuid();

    /// <summary>Creates a parent entity and returns its id, so branches have a valid root.</summary>
    private static async Task<Guid> SeedEntityAsync(EntityModuleHarness h)
    {
        var created = await new CreateEntityCommandHandler(h.EntityRepo, h.TypeRepo, h.Uow)
            .Handle(new CreateEntityCommand(Workspace, "Parent Pharmacy", "pharmacy"), default);
        return created.Value!.Id;
    }

    [Fact]
    public async Task CreateBranch_UnderExistingEntity_Succeeds()
    {
        using var h = new EntityModuleHarness();
        var entityId = await SeedEntityAsync(h);

        var result = await new CreateBranchCommandHandler(h.BranchRepo, h.EntityRepo, h.Uow)
            .Handle(new CreateBranchCommand(entityId, "Downtown", "1 Tahrir St", "Cairo", "Cairo", "+20100"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.City.Should().Be("Cairo");
        result.Value.EntityRootId.Should().Be(entityId);

        await using var verify = h.NewContext();
        (await verify.Branches.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateBranch_MissingParentEntity_FailsWithEntityNotFound()
    {
        using var h = new EntityModuleHarness();

        var result = await new CreateBranchCommandHandler(h.BranchRepo, h.EntityRepo, h.Uow)
            .Handle(new CreateBranchCommand(Guid.NewGuid(), "Orphan"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("Entity.NotFound");
    }

    [Fact]
    public async Task ListBranches_FiltersByEntity()
    {
        using var h = new EntityModuleHarness();
        var entityId = await SeedEntityAsync(h);
        var handler = new CreateBranchCommandHandler(h.BranchRepo, h.EntityRepo, h.Uow);
        await handler.Handle(new CreateBranchCommand(entityId, "B1"), default);
        await handler.Handle(new CreateBranchCommand(entityId, "B2"), default);

        var result = await new ListBranchesQueryHandler(h.BranchRepo)
            .Handle(new ListBranchesQuery(entityId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateBranch_ChangesFields()
    {
        using var h = new EntityModuleHarness();
        var entityId = await SeedEntityAsync(h);
        var created = await new CreateBranchCommandHandler(h.BranchRepo, h.EntityRepo, h.Uow)
            .Handle(new CreateBranchCommand(entityId, "Old"), default);

        var update = await new UpdateBranchCommandHandler(h.BranchRepo, h.Uow)
            .Handle(new UpdateBranchCommand(created.Value!.Id, "New", "Addr", "Giza", "Giza", "+20111"), default);

        update.IsSuccess.Should().BeTrue();
        update.Value!.Name.Should().Be("New");
        update.Value.City.Should().Be("Giza");
    }

    [Fact]
    public async Task DeactivateThenReactivateBranch_TogglesVisibility()
    {
        using var h = new EntityModuleHarness();
        var entityId = await SeedEntityAsync(h);
        var created = await new CreateBranchCommandHandler(h.BranchRepo, h.EntityRepo, h.Uow)
            .Handle(new CreateBranchCommand(entityId, "Temp"), default);
        var id = created.Value!.Id;

        await new DeactivateBranchCommandHandler(h.BranchRepo, h.Uow)
            .Handle(new DeactivateBranchCommand(id), default);
        var hidden = await new ListBranchesQueryHandler(h.BranchRepo)
            .Handle(new ListBranchesQuery(entityId), default);
        hidden.Value!.Should().BeEmpty();

        await new ReactivateBranchCommandHandler(h.BranchRepo, h.Uow)
            .Handle(new ReactivateBranchCommand(id), default);
        var shown = await new ListBranchesQueryHandler(h.BranchRepo)
            .Handle(new ListBranchesQuery(entityId), default);
        shown.Value!.Should().ContainSingle().Which.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateBranch_UnknownId_ReturnsNotFound()
    {
        using var h = new EntityModuleHarness();

        var result = await new UpdateBranchCommandHandler(h.BranchRepo, h.Uow)
            .Handle(new UpdateBranchCommand(Guid.NewGuid(), "X", null, null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NotFound");
    }
}
