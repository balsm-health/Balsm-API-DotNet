using Balsm.SharedKernel.Domain;

namespace Balsm.Infrastructure.Lifecycle;

public sealed class MigrationStateRecord : BaseEntity
{
    public string DbContextName { get; set; } = "";
    public string MigrationId { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
}
