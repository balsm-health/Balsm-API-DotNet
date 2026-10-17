using Balsm.SharedKernel.Domain;

namespace Balsm.Infrastructure.Platform;

public sealed class ServerConfigEntry : BaseEntity
{
    public ServerConfigEntry() { }

    internal ServerConfigEntry(Guid id)
    {
        Id = id;
    }

    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
