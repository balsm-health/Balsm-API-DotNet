namespace Balsm.Supervisor.Models;

public sealed class ApiStatusResponse
{
    public bool IsRunning { get; set; }
    public int? Pid { get; set; }
    public DateTime? StartedAt { get; set; }
    public TimeSpan? Uptime { get; set; }
    public string? Mode { get; set; }
    public string? ApiUrl { get; set; }
    public string? Version { get; set; }

    /// <summary>Operating system description (e.g. "Ubuntu 22.04 LTS").</summary>
    public string? Os { get; set; }

    /// <summary>HTTP listen port.</summary>
    public int HttpPort { get; set; }

    /// <summary>HTTPS (admin panel) listen port.</summary>
    public int HttpsPort { get; set; }

    /// <summary>On-disk size of the SQLite database file, in bytes.</summary>
    public long? DbSizeBytes { get; set; }

    /// <summary>Colon-delimited hex SHA-256 fingerprint of the self-signed TLS certificate.</summary>
    public string? CertSha256 { get; set; }
}
