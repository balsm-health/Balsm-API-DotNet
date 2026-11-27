using Balsm.API.Tests.Platform;
using Balsm.Infrastructure.Audit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.API.Tests.Audit;

public sealed class AuditLogWriterTests : IDisposable
{
    private readonly PlatformDbHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task WriteAsync_Row_PersistsIt()
    {
        // Sequence is set explicitly: SQLite has no generator for it.
        var row = new AuditLog { Sequence = 1, Module = "Account", Action = "Created", OccurredAt = DateTime.UtcNow };

        await new AuditLogWriter(_harness.NewContext()).WriteAsync(row);

        (await _harness.NewContext().AuditLogs.SingleAsync()).Id.Should().Be(row.Id);
    }
}
