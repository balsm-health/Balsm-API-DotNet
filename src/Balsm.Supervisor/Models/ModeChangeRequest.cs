namespace Balsm.Supervisor.Models;

public sealed class ModeChangeRequest
{
    public string Mode { get; set; } = "local";
    public int Port { get; set; } = 5000;
}
