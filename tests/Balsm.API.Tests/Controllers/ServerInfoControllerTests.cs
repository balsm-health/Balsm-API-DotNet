using Balsm.API.Controllers;
using Balsm.Entity.Application.DTOs;
using Balsm.SharedKernel.Results;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Balsm.API.Tests.Controllers;

public class ServerInfoControllerTests
{
    // Minimal IMediator stub: GetWorkspaceQuery returns NotFound (pre-setup state).
    private sealed class StubMediator(bool workspaceExists) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            object result = workspaceExists
                ? Result.Success(new WorkspaceDto(Guid.NewGuid(), "Cairo Pharmacy", "cairo", "Active", "ar"))
                : Result.Failure<WorkspaceDto>(Error.NotFound);
            return Task.FromResult((TResponse)result);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    private static async Task<object> GetPayloadAsync(bool workspaceExists = false)
    {
        var result = await new ServerInfoController(new StubMediator(workspaceExists)).Get(default);
        return result.Should().BeOfType<OkObjectResult>().Subject.Value!;
    }

    private static object? Prop(object value, string name)
        => value.GetType().GetProperty(name)?.GetValue(value);

    [Fact]
    public async Task Get_ReturnsAllContractFields()
    {
        var payload = await GetPayloadAsync();

        Prop(payload, "version").Should().BeOfType<string>();
        Prop(payload, "mode").Should().NotBeNull();
        Prop(payload, "workspace_name").Should().BeOfType<string>();
        Prop(payload, "certificate_sha256").Should().BeOfType<string>();
        Prop(payload, "uptime_seconds").Should().BeOfType<long>();
    }

    [Fact]
    public async Task Get_DefaultsToStandaloneMode()
    {
        Environment.SetEnvironmentVariable("DeploymentMode", null);

        var payload = await GetPayloadAsync();

        Prop(payload, "mode").Should().Be("Standalone");
    }

    [Fact]
    public async Task Get_PreSetup_WorkspaceNameIsEmpty()
    {
        var payload = await GetPayloadAsync(workspaceExists: false);

        Prop(payload, "workspace_name").Should().Be(string.Empty);
    }

    [Fact]
    public async Task Get_AfterSetup_ReturnsWorkspaceName()
    {
        var payload = await GetPayloadAsync(workspaceExists: true);

        Prop(payload, "workspace_name").Should().Be("Cairo Pharmacy");
    }

    [Fact]
    public async Task Get_UptimeIsNonNegative()
    {
        var payload = await GetPayloadAsync();

        ((long)Prop(payload, "uptime_seconds")!).Should().BeGreaterThanOrEqualTo(0);
    }
}
