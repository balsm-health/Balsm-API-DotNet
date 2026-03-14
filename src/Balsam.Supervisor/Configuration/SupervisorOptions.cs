namespace Balsam.Supervisor.Configuration;

public sealed class SupervisorOptions
{
    public const string SectionName = "Supervisor";

    public string GitHubRepository { get; set; } = "AskBalsam/Balsam-API-DotNet";

    public bool EnableMdns { get; set; } = true;

    public string MdnsHostname { get; set; } = "balsam";

    public string ConnectionInfoPath { get; set; } = "connection-info.txt";

    public string FirstRunSentinelPath { get; set; } = ".first-run-done";

    public string CredentialsPath { get; set; } = "admin-credentials.json";

    public bool EnableTunnel { get; set; } = false;

    public string? CloudflaredPath { get; set; }

    public string? TunnelToken { get; set; }

    public string? ServerId { get; set; }

    public string RegistryUrl { get; set; } = "https://registry.balsam.cloud";

    public string? RegistrySecret { get; set; }

    public string? TunnelUrl { get; set; }
}
