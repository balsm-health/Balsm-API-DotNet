using Balsm.Identity.Application.Commands;
using Balsm.Identity.Application.DTOs;
using Balsm.Identity.Domain;
using Balsm.SharedKernel.Results;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.Identity.Tests;

public sealed class UpdateCurrentAdminUserCommandHandlerTests : IDisposable
{
    private readonly FakeIdentityStore _store = new();
    private readonly ServiceProvider _provider;
    private readonly AdminUserMirror _user = AdminUserMirror.Create("admin@example.test", "Test Admin");

    public UpdateCurrentAdminUserCommandHandlerTests() => _provider = HandlerHost.Build(_store);

    public void Dispose() => _provider.Dispose();

    [Fact]
    public async Task Handle_WhenUserExists_ReturnsUpdatedDto()
    {
        _store.Seed(_user);

        var result = await _provider.Sender().Send(
            new UpdateCurrentAdminUserCommand("admin@example.test", "  Renamed Admin ", "ar"));

        result.Value.Should().Be(new AdminUserDto(
            _user.Id, "admin@example.test", "Renamed Admin", "SystemAdmin", "ar", null, _user.PasswordChangedAt));
    }

    [Fact]
    public async Task Handle_WhenUserExists_UpdatesAndSavesOnce()
    {
        _store.Seed(_user);

        await _provider.Sender().Send(new UpdateCurrentAdminUserCommand("admin@example.test", "Renamed Admin", "ar"));

        _store.Should().BeEquivalentTo(new { Updated = new[] { _user }, SaveCount = 1 }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Handle_WhenUserMissing_ReturnsNotFoundError()
    {
        var result = await _provider.Sender().Send(
            new UpdateCurrentAdminUserCommand("missing@example.test", "Renamed Admin", "ar"));

        result.Should().BeEquivalentTo(new
        {
            IsSuccess = false,
            Error = new Error("Identity.NotFound", "Admin user not found."),
        });
    }

    [Fact]
    public async Task Handle_WhenUserMissing_DoesNotSave()
    {
        await _provider.Sender().Send(new UpdateCurrentAdminUserCommand("missing@example.test", "Renamed Admin", "ar"));

        _store.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithBlankDisplayName_ThrowsArgumentException()
    {
        _store.Seed(_user);

        var act = () => _provider.Sender().Send(new UpdateCurrentAdminUserCommand("admin@example.test", " ", "ar"));

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
