namespace Balsam.Supervisor.Models;

public sealed class ApiStatusResponse
{
    public bool IsRunning { get; set; }
    public int? Pid { get; set; }
    public DateTime? StartedAt { get; set; }
    public TimeSpan? Uptime { get; set; }
    public string? Mode { get; set; }
    public string? ApiUrl { get; set; }
    public string? Version { get; set; }
}
