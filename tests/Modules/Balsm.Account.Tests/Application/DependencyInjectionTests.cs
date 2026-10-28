using Balsm.Account.Application;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balsm.Account.Tests.Application;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddAccountApplication_Always_RegistersMediator()
    {
        var services = new ServiceCollection();

        services.AddAccountApplication();

        services.Should().Contain(d => d.ServiceType == typeof(IMediator));
    }
}
