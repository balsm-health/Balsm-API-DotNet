using Balsm.Entity.Application.Commands;
using Balsm.Entity.Application.Handlers;
using Balsm.Entity.Application.Queries;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.API.Tests.Entity;

public class EntityHandlerTests
{
    private static readonly Guid Workspace = Guid.NewGuid();

    private static CreateEntityCommandHandler CreateHandler(EntityModuleHarness h)
        => new(h.EntityRepo, h.TypeRepo, h.Uow);

    [Fact]
    public async Task CreateEntity_WithSeededType_Succeeds()
    {
        using var h = new EntityModuleHarness();

        var result = await CreateHandler(h)
            .Handle(new CreateEntityCommand(Workspace, "El-Nahda Pharmacy", "pharmacy", "REG-123"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TypeCode.Should().Be("pharmacy");
        result.Value.RegistrationNumber.Should().Be("REG-123");

        await using var verify = h.NewContext();
        (await verify.Entities.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateEntity_UnknownType_FailsWithEntityTypeNotFound()
    {
        using var h = new EntityModuleHarness();

        var result = await CreateHandler(h)
            .Handle(new CreateEntityCommand(Workspace, "Bad", "does-not-exist"), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("EntityType.NotFound");
    }

    [Fact]
    public async Task GetEntityById_UnknownId_ReturnsNotFound()
    {
        using var h = new EntityModuleHarness();

        var result = await new GetEntityByIdQueryHandler(h.EntityRepo)
            .Handle(new GetEntityByIdQuery(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task ListEntities_FiltersByWorkspace()
    {
        using var h = new EntityModuleHarness();
        var other = Guid.NewGuid();
        await CreateHandler(h).Handle(new CreateEntityCommand(Workspace, "A", "pharmacy"), default);
        await CreateHandler(h).Handle(new CreateEntityCommand(Workspace, "B", "clinic"), default);
        await CreateHandler(h).Handle(new CreateEntityCommand(other, "C", "hospital"), default);

        var result = await new ListEntitiesQueryHandler(h.EntityRepo)
            .Handle(new ListEntitiesQuery(Workspace), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(2).And.OnlyContain(e => e.WorkspaceId == Workspace);
    }

    [Fact]
    public async Task DeactivateEntity_SoftDeletes_HiddenByDefault_VisibleWithIncludeInactive()
    {
        using var h = new EntityModuleHarness();
        var created = await CreateHandler(h).Handle(new CreateEntityCommand(Workspace, "Temp", "pharmacy"), default);
        var id = created.Value!.Id;

        var deactivate = await new DeactivateEntityCommandHandler(h.EntityRepo, h.Uow)
            .Handle(new DeactivateEntityCommand(id), default);
        deactivate.IsSuccess.Should().BeTrue();

        var activeList = await new ListEntitiesQueryHandler(h.EntityRepo)
            .Handle(new ListEntitiesQuery(Workspace), default);
        activeList.Value!.Should().BeEmpty("soft-deleted entities are filtered by the global query filter");

        var allList = await new ListEntitiesQueryHandler(h.EntityRepo)
            .Handle(new ListEntitiesQuery(Workspace, IncludeInactive: true), default);
        allList.Value!.Should().ContainSingle().Which.IsDeleted.Should().BeTrue();

        await using var verify = h.NewContext();
        var row = await verify.Entities.IgnoreQueryFilters().SingleAsync();
        row.IsDeleted.Should().BeTrue();
        row.DeletedAt.Should().NotBeNull("soft-delete audit stamp is set on save");
    }

    [Fact]
    public async Task ReactivateEntity_RestoresSoftDeletedEntity()
    {
        using var h = new EntityModuleHarness();
        var created = await CreateHandler(h).Handle(new CreateEntityCommand(Workspace, "Temp", "pharmacy"), default);
        var id = created.Value!.Id;
        await new DeactivateEntityCommandHandler(h.EntityRepo, h.Uow)
            .Handle(new DeactivateEntityCommand(id), default);

        var reactivate = await new ReactivateEntityCommandHandler(h.Uow, h.EntityRepo)
            .Handle(new ReactivateEntityCommand(id), default);

        reactivate.IsSuccess.Should().BeTrue();
        var activeList = await new ListEntitiesQueryHandler(h.EntityRepo)
            .Handle(new ListEntitiesQuery(Workspace), default);
        activeList.Value!.Should().ContainSingle().Which.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task ReactivateEntity_UnknownId_ReturnsNotFound()
    {
        using var h = new EntityModuleHarness();

        var result = await new ReactivateEntityCommandHandler(h.Uow, h.EntityRepo)
            .Handle(new ReactivateEntityCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task UpdateEntity_ChangesNameAndRegistration()
    {
        using var h = new EntityModuleHarness();
        var created = await CreateHandler(h).Handle(new CreateEntityCommand(Workspace, "Old", "pharmacy", "OLD"), default);

        var update = await new UpdateEntityCommandHandler(h.EntityRepo, h.Uow)
            .Handle(new UpdateEntityCommand(created.Value!.Id, "New", "NEW"), default);

        update.IsSuccess.Should().BeTrue();
        update.Value!.Name.Should().Be("New");
        update.Value.RegistrationNumber.Should().Be("NEW");
    }

    [Fact]
    public async Task ListEntityTypes_ReturnsSeededReferenceData()
    {
        using var h = new EntityModuleHarness();

        var result = await new ListEntityTypesQueryHandler(h.TypeRepo)
            .Handle(new ListEntityTypesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(t => t.Code)
            .Should().BeEquivalentTo(["pharmacy", "clinic", "hospital"]);
        result.Value.Should().OnlyContain(t => t.IsSeeded);
    }
}
