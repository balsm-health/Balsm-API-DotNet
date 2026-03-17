using Balsm.Supervisor.Models;

namespace Balsm.Supervisor.Services;

public interface IFederationStore
{
    Task<FederationData> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(FederationData data, CancellationToken ct = default);
}
