using Balsm.Identity.Domain;
using Balsm.Identity.Domain.Repositories;

namespace Balsm.Identity.Tests;

/// <summary>In-memory stand-in for the repository and unit of work, for handler unit tests.</summary>
internal sealed class FakeIdentityStore : IAdminUserMirrorRepository, IIdentityUnitOfWork
{
    private readonly List<AdminUserMirror> _users = [];

    public List<AdminUserMirror> Updated { get; } = [];

    public int SaveCount { get; private set; }

    public void Seed(AdminUserMirror user) => _users.Add(user);

    public Task<AdminUserMirror?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Email == email.ToLowerInvariant()));

    public Task<AdminUserMirror?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<AdminUserMirror>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AdminUserMirror>>(_users);

    public Task AddAsync(AdminUserMirror entity, CancellationToken cancellationToken = default)
    {
        _users.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(AdminUserMirror entity) => Updated.Add(entity);

    public void Remove(AdminUserMirror entity) => _users.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.FromResult(Updated.Count);
    }
}
