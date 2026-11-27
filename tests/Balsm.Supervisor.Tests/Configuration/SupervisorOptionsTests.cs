using Balsm.Supervisor.Configuration;
using FluentAssertions;
using Xunit;

namespace Balsm.Supervisor.Tests.Configuration;

public class SupervisorOptionsTests
{
    [Fact]
    public void Constructor_AppliesSafeDefaults()
    {
        var sut = new SupervisorOptions();

        sut.Should().BeEquivalentTo(new
        {
            GitHubRepository = "AskBalsm/Balsm-API-DotNet",
            EnableMdns = true,
            MdnsHostname = "balsm",
            ConnectionInfoPath = "connection-info.txt",
            FirstRunSentinelPath = ".first-run-done",
            CredentialsPath = "admin-credentials.json",
            EnableTunnel = false,
            CloudflaredPath = (string?)null,
            TunnelToken = (string?)null,
            ServerId = (string?)null,
            RegistryUrl = "https://registry.balsm.cloud",
            RegistrySecret = (string?)null,
            TunnelUrl = (string?)null,
            FederationDataPath = "federation-pairings.json",
            LocalCliTokenPath = "local-cli.token"
        });
    }

    [Fact]
    public void SectionName_IsSupervisor()
    {
        var name = SupervisorOptions.SectionName;

        name.Should().Be("Supervisor");
    }
}
