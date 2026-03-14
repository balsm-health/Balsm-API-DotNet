namespace Balsam.Supervisor.Configuration;

public sealed class SupervisorOptions
{
    public const string SectionName = "Supervisor";

    public string GitHubRepository { get; set; } = "AskBalsam/Balsam-API-DotNet";

    public bool EnableMdns { get; set; } = true;

    public string MdnsHostname { get; set; } = "balsam";

    public string ConnectionInfoPath { get; set; } = "connection-info.txt";

    public string FirstRunSentinelPath { get; set; } = ".first-run-done";
}
