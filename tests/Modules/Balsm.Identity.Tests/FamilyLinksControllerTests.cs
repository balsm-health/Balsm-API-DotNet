using System.Security.Claims;
using Balsm.Identity.Api.Controllers;
using Balsm.Identity.Api.FamilyLinks;
using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.Identity.Application.FamilyLinks.Queries;
using Balsm.Identity.Application.FamilyLinks.Validators;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Infrastructure.RateLimit;
using Balsm.SharedKernel.Pagination;
using Balsm.SharedKernel.Results;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using static Balsm.Identity.Tests.FamilyLinkFixtures;

namespace Balsm.Identity.Tests;

public sealed class FamilyLinksControllerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private static readonly FamilyLinkDto Dto = new(
        LinkId, "outgoing", "pending", "child", null, null, null, null, null, Now, null, null, null);

    private FamilyLinksController Controller(Guid userId)
    {
        var controller = new FamilyLinksController(_mediator);
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())])),
        };
        http.Items["CorrelationId"] = "corr-1";
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    [Theory]
    [InlineData(true, StatusCodes.Status201Created)]
    [InlineData(false, StatusCodes.Status200OK)]
    public async Task Create_DistinguishesNewFromReplay(bool created, int expected)
    {
        _mediator.Send(Arg.Any<CreateFamilyLinkCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CreateFamilyLinkResult(Dto, created)));

        var response = await Controller(Requester).Create(
            new CreateFamilyLinkRequest(LinkId, Jti, "child", null), new CreateFamilyLinkCommandValidator(), CancellationToken.None);

        ((IStatusCodeActionResult)response).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Create_TakesTheRequesterFromTheToken()
    {
        _mediator.Send(Arg.Any<CreateFamilyLinkCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new CreateFamilyLinkResult(Dto, true)));

        await Controller(Requester).Create(
            new CreateFamilyLinkRequest(LinkId, Jti, "child", null), new CreateFamilyLinkCommandValidator(), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<CreateFamilyLinkCommand>(c => c.RequesterUserId == Requester && c.CorrelationId == "corr-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_InvalidBody_Answers422WithSnakeCaseFieldsAndNeverReachesTheHandler()
    {
        var response = await Controller(Requester).Create(
            new CreateFamilyLinkRequest(LinkId, Jti, "cousin", new byte[2]), new CreateFamilyLinkCommandValidator(), CancellationToken.None);

        var result = response.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var error = result.Value.Should().BeOfType<FamilyLinkErrorResponse>().Subject.Error;
        error.Fields!.Select(f => f.Field).Should().BeEquivalentTo("relation", "public_key");
        error.CorrelationId.Should().Be("corr-1");
        await _mediator.DidNotReceive().Send(Arg.Any<CreateFamilyLinkCommand>(), Arg.Any<CancellationToken>());
    }

    public static TheoryData<string, int> ErrorStatuses => new()
    {
        { FamilyLinkErrors.NotFound.Code, 404 },
        { FamilyLinkErrors.SelfLink.Code, 409 },
        { FamilyLinkErrors.AlreadyExists.Code, 409 },
        { FamilyLinkErrors.NotPending.Code, 409 },
        { FamilyLinkErrors.NotLinked.Code, 409 },
        { FamilyLinkErrors.RecipientMustDecline.Code, 409 },
        { FamilyLinkErrors.Ended.Code, 409 },
        { FamilyLinkErrors.ConcurrentUpdate.Code, 409 },
        { FamilyLinkErrors.StaleCardVersion.Code, 409 },
        { FamilyLinkErrors.CardTooLarge.Code, 413 },
        { FamilyLinkErrors.InvalidRelation.Code, 422 },
        { FamilyLinkErrors.InvalidId.Code, 422 },
        { FamilyLinkErrors.InvalidPublicKey.Code, 422 },
        { FamilyLinkErrors.InvalidCard.Code, 422 },
        { "Something.Else", 400 },
    };

    [Theory]
    [MemberData(nameof(ErrorStatuses))]
    public async Task Failures_MapToTheirContractStatus(string code, int expected)
    {
        _mediator.Send(Arg.Any<UpsertFamilyLinkCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error(code, "x")));

        var response = await Controller(Requester).UpsertCard(
            LinkId, new UpsertFamilyLinkCardRequest(1, Key(), Nonce(), Ciphertext()),
            new UpsertFamilyLinkCardCommandValidator(), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Approve_WithNoBody_SendsNoKey()
    {
        _mediator.Send(Arg.Any<ApproveFamilyLinkCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(Dto));

        var response = await Controller(Recipient).Approve(LinkId, null, new ApproveFamilyLinkCommandValidator(), CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        await _mediator.Received(1).Send(
            Arg.Is<ApproveFamilyLinkCommand>(c => c.PublicKey == null && c.UserId == Recipient), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_BadKey_Answers422()
    {
        var response = await Controller(Recipient).Approve(
            LinkId, new ApproveFamilyLinkRequest(new byte[3]), new ApproveFamilyLinkCommandValidator(), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task Approve_Failure_MapsStatus()
    {
        _mediator.Send(Arg.Any<ApproveFamilyLinkCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<FamilyLinkDto>(FamilyLinkErrors.NotPending));

        var response = await Controller(Recipient).Approve(LinkId, null, new ApproveFamilyLinkCommandValidator(), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(409);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Decline_MapsSuccessAndFailure(bool success)
    {
        _mediator.Send(Arg.Any<DeclineFamilyLinkCommand>(), Arg.Any<CancellationToken>())
            .Returns(success ? Result.Success(Dto) : Result.Failure<FamilyLinkDto>(FamilyLinkErrors.NotFound));

        var response = await Controller(Recipient).Decline(LinkId, CancellationToken.None);

        ((IStatusCodeActionResult)response).StatusCode.Should().Be(success ? 200 : 404);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Get_MapsSuccessAndFailure(bool success)
    {
        _mediator.Send(Arg.Any<GetFamilyLinkByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(success ? Result.Success(Dto) : Result.Failure<FamilyLinkDto>(FamilyLinkErrors.NotFound));

        var response = await Controller(Requester).Get(LinkId, CancellationToken.None);

        ((IStatusCodeActionResult)response).StatusCode.Should().Be(success ? 200 : 404);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteAndDeleteCard_Answer204OrTheFailure(bool success)
    {
        var outcome = success ? Result.Success() : Result.Failure(FamilyLinkErrors.NotFound);
        _mediator.Send(Arg.Any<DeleteFamilyLinkCommand>(), Arg.Any<CancellationToken>()).Returns(outcome);
        _mediator.Send(Arg.Any<DeleteFamilyLinkCardCommand>(), Arg.Any<CancellationToken>()).Returns(outcome);
        var controller = Controller(Requester);

        var link = await controller.Delete(LinkId, CancellationToken.None);
        var card = await controller.DeleteCard(LinkId, CancellationToken.None);

        var expected = success ? 204 : 404;
        (((IStatusCodeActionResult)link).StatusCode, ((IStatusCodeActionResult)card).StatusCode).Should().Be(((int?)expected, (int?)expected));
    }

    [Theory]
    [InlineData(32, 204)]
    [InlineData(4, 422)]
    public async Task UpdateKey_ValidatesThenAnswers204(int keyLength, int expected)
    {
        _mediator.Send(Arg.Any<UpdateFamilyLinkKeyCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());

        var response = await Controller(Requester).UpdateKey(
            LinkId, new UpdateFamilyLinkKeyRequest(new byte[keyLength]), new UpdateFamilyLinkKeyCommandValidator(), CancellationToken.None);

        ((IStatusCodeActionResult)response).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task UpsertCard_Malformed_Answers422()
    {
        var response = await Controller(Requester).UpsertCard(
            LinkId, new UpsertFamilyLinkCardRequest(0, Key(), Nonce(), Ciphertext()),
            new UpsertFamilyLinkCardCommandValidator(), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task GetCard_Success_IsNeverCached()
    {
        _mediator.Send(Arg.Any<GetFamilyLinkCardQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(
            new FamilyLinkCardDto(LinkId, Recipient, 1, Key(), Nonce(), Ciphertext(), Now)));
        var controller = Controller(Requester);

        var response = await controller.GetCard(LinkId, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<FamilyLinkCardResponse>();
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
    }

    [Fact]
    public async Task GetCard_Failure_Answers404()
    {
        _mediator.Send(Arg.Any<GetFamilyLinkCardQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<FamilyLinkCardDto>(FamilyLinkErrors.NotFound));

        var response = await Controller(Requester).GetCard(LinkId, CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Lists_WrapThePageInTheEnvelope()
    {
        var page = new PagedList<FamilyLinkDto>([Dto], 3, 2, 1);
        _mediator.Send(Arg.Any<ListIncomingFamilyLinksQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(page));
        _mediator.Send(Arg.Any<ListOutgoingFamilyLinksQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(page));
        var controller = Controller(Recipient);

        var incoming = await controller.ListIncoming("pending", new ListIncomingFamilyLinksQueryValidator(), CancellationToken.None, 2, 1);
        var outgoing = await controller.ListOutgoing(null, new ListOutgoingFamilyLinksQueryValidator(), CancellationToken.None, 2, 1);

        foreach (var response in new[] { incoming, outgoing })
        {
            response.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<FamilyLinkListResponse>()
                .Which.Data.Should().Match<FamilyLinkPageDto>(p => p.Page == 2 && p.PageSize == 1 && p.TotalCount == 3 && p.HasNextPage);
        }
    }

    [Fact]
    public async Task Lists_BadStatus_Answers422()
    {
        var response = await Controller(Recipient).ListIncoming("nope", new ListIncomingFamilyLinksQueryValidator(), CancellationToken.None);

        response.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(422);
    }

    [Fact]
    public async Task RateLimit_RefusesTheRequestAfterTheLimitWithRetryAfter()
    {
        var services = new ServiceCollection()
            .AddSingleton<IRateLimitStore>(new InMemoryRateLimitStore(new MemoryCache(new MemoryCacheOptions())))
            .BuildServiceProvider();
        var attribute = new FamilyLinkCreateRateLimitAttribute();
        var nextCalls = 0;
        ResourceExecutingContext NewContext()
        {
            var http = new DefaultHttpContext
            {
                RequestServices = services,
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Requester.ToString())])),
            };
            return new ResourceExecutingContext(
                new ActionContext(http, new RouteData(), new ActionDescriptor()), [], []);
        }

        ResourceExecutingContext last = null!;
        for (var i = 0; i <= FamilyLinkCreateRateLimitAttribute.Limit; i++)
        {
            last = NewContext();
            await attribute.OnResourceExecutionAsync(last, () =>
            {
                nextCalls++;
                return Task.FromResult<ResourceExecutedContext>(null!);
            });
        }

        nextCalls.Should().Be(FamilyLinkCreateRateLimitAttribute.Limit);
        last.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(429);
        last.HttpContext.Response.Headers.RetryAfter.ToString().Should().NotBeEmpty();
    }
}
