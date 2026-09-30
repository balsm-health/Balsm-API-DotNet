using Balsm.CareTeam.Api.Controllers;
using Balsm.CareTeam.Application.Commands;
using Balsm.CareTeam.Application.Queries;
using Balsm.CareTeam.Domain;
using Balsm.SharedKernel.Results;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace Balsm.CareTeam.Tests;

public sealed class CareTeamControllerTests
{
    private static CareTeamController Controller(IMediator mediator, Guid userId)
    {
        var controller = new CareTeamController(mediator);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())]);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static UpsertCareProviderRequest Request(Guid id, Guid profileId) =>
        new(id, profileId, "doctor", "Provider Alpha",
            null, null, null, null, null, null, null, null, DateTime.UtcNow);

    [Fact]
    public async Task Upsert_TakesUserIdFromTokenNotBody()
    {
        var tokenUser = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpsertCareProviderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await Controller(mediator, tokenUser)
            .Upsert(Request(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<UpsertCareProviderCommand>(c => c.UserId == tokenUser),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upsert_OnTombstoned_Returns409()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpsertCareProviderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(CareTeamErrors.Tombstoned));

        var response = await Controller(mediator, Guid.NewGuid())
            .Upsert(Request(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        response.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Upsert_InvalidType_Returns422()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpsertCareProviderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(CareTeamErrors.InvalidType));

        var response = await Controller(mediator, Guid.NewGuid())
            .Upsert(Request(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        response.Should().BeOfType<UnprocessableEntityObjectResult>();
    }

    /// <summary>Review Focus 4 — another user's row answers 404, never 403.</summary>
    [Fact]
    public async Task Upsert_NotOwned_Returns404()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<UpsertCareProviderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(CareTeamErrors.NotFound));

        var response = await Controller(mediator, Guid.NewGuid())
            .Upsert(Request(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_NotOwned_Returns404()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeleteCareProviderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(CareTeamErrors.NotFound));

        var response = await Controller(mediator, Guid.NewGuid())
            .Delete(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_Success_Returns204()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeleteCareProviderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var response = await Controller(mediator, Guid.NewGuid())
            .Delete(Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_TakesUserIdFromToken()
    {
        var tokenUser = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<DeleteCareProviderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await Controller(mediator, tokenUser).Delete(Guid.NewGuid(), CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<DeleteCareProviderCommand>(c => c.UserId == tokenUser),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pull_PassesProfileScopeThrough()
    {
        var profileId = Guid.NewGuid();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<PullCareProvidersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CareProviderDto>>([]));

        await Controller(mediator, Guid.NewGuid()).Pull(profileId, null, CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<PullCareProvidersQuery>(q => q.HealthProfileId == profileId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pull_PassesSinceCursorThrough()
    {
        var since = DateTime.UtcNow.AddDays(-1);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<PullCareProvidersQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CareProviderDto>>([]));

        await Controller(mediator, Guid.NewGuid()).Pull(Guid.NewGuid(), since, CancellationToken.None);

        await mediator.Received(1).Send(
            Arg.Is<PullCareProvidersQuery>(q => q.Since == since),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Health_ReportsHealthyForThisModule()
    {
        var response = new HealthController().Get();

        response.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)response).Value!.ToString();
        payload.Should().Contain("Healthy").And.Contain("CareTeam");
    }
}
