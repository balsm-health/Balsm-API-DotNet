namespace Balsam.Supervisor.Models;

public sealed class UpdateInfoResponse
{
    public string CurrentVersion { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public bool IsUpdateAvailable { get; set; }
    public string? ReleaseUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ReleaseNotes { get; set; }
}
