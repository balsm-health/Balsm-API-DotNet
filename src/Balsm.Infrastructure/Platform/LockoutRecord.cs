using Balsm.SharedKernel.Domain;

namespace Balsm.Infrastructure.Platform;

public sealed class LockoutRecord : BaseEntity
{
    public string AdminEmail { get; set; } = "";
    public string SourceIp { get; set; } = "";
    public int FailedAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
}
