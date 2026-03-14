namespace Balsam.Supervisor.Models;

public sealed class NetworkInfoResponse
{
    public string Hostname { get; set; } = "";
    public List<LanAddress> LanAddresses { get; set; } = [];
    public string? MdnsHostname { get; set; }
    public string? MdnsApiUrl { get; set; }
    public bool MdnsRegistered { get; set; }
    public string? PublicIp { get; set; }
}

public sealed class LanAddress
{
    public string IpAddress { get; set; } = "";
    public string InterfaceName { get; set; } = "";
    public string InterfaceType { get; set; } = "";
    public string ApiUrl { get; set; } = "";
    public string AdminUrl { get; set; } = "";
}
