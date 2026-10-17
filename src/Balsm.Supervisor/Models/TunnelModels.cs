namespace Balsm.Supervisor.Models;

public sealed class TunnelStatusResponse
{
    public bool IsRunning { get; set; }
    public string? TunnelUrl { get; set; }
    public string? TunnelType { get; set; }
    public string? Error { get; set; }
    public bool CloudflaredInstalled { get; set; }
    public string? RegisteredUrl { get; set; }
    public string? ServerId { get; set; }
}

public sealed class TunnelStartRequest
{
    public string Type { get; set; } = "quick";
    public string? Token { get; set; }
}

public sealed class TunnelTokenRequest
{
    public string Token { get; set; } = "";
}

public sealed class TunnelRegistryResponse
{
    public string TunnelToken { get; set; } = "";
    public string TunnelUrl { get; set; } = "";
    public string TunnelId { get; set; } = "";
    public string ServerId { get; set; } = "";
}
