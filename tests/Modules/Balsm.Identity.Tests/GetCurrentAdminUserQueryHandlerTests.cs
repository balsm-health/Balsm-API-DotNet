using Balsm.Identity.Application.DTOs;
using Balsm.Identity.Application.Queries;
using Balsm.Identity.Domain;
using Balsm.SharedKernel.Results;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.Identity.Tests;

public sealed class GetCurrentAdminUserQueryHandlerTests : IDisposable
{
    private readonly FakeIdentityStore _store = new();
    private readonly ServiceProvider _provider;

    public GetCurrentAdminUserQueryHandlerTests() => _provider = HandlerHost.Build(_store);

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Handle_WhenUserExists_ReturnsSuccess()
    {
        _store.Seed(AdminUserMirror.Create("admin@example.test", "Test Admin"));

        var result = await _provider.Sender().Send(new GetCurrentAdminUserQuery("admin@example.test"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenUserExists_MapsUserToDto()
    {
        var user = AdminUserMirror.Create("admin@example.test", "Test Admin", "ar");
        user.RecordLogin();
        _store.Seed(user);

        var result = await _provider.Sender().Send(new GetCurrentAdminUserQuery("Admin@Example.Test"));

        result.Value.Should().Be(new AdminUserDto(
            user.Id, "admin@example.test", "Test Admin", "SystemAdmin", "ar", user.LastLoginAt, user.PasswordChangedAt));
    }

    [Fact]
    public async Task Handle_WhenUserMissing_ReturnsNotFoundError()
    {
        var result = await _provider.Sender().Send(new GetCurrentAdminUserQuery("missing@example.test"));

        result.Should().BeEquivalentTo(new
        {
            IsSuccess = false,
            Error = new Error("Identity.NotFound", "Admin user not found."),
        });
    }
}
