using Balsm.SharedKernel.Contracts;

namespace Balsm.Auth.Tests;

/// Stands in for the Deletion module's registry: an id is deleted when listed.
internal sealed class FakeDeletedAccountRegistry : IDeletedAccountRegistry
{
    public HashSet<Guid> Deleted { get; } = [];

    public Task<bool> WasDeletedAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(Deleted.Contains(userId));
}
