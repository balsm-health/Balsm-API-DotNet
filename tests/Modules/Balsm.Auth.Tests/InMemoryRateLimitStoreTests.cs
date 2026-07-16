using Balsm.Infrastructure.RateLimit;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// Unit tests for the Standalone-mode rate-limit store (FR-045 backing store).
/// </summary>
public sealed class InMemoryRateLimitStoreTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly InMemoryRateLimitStore _store;

    public InMemoryRateLimitStoreTests() => _store = new InMemoryRateLimitStore(_cache);

    [Fact]
    public async Task TryConsume_UnderLimit_Allows()
    {
        for (var i = 0; i < 3; i++)
        {
            var decision = await _store.TryConsumeAsync("k", 3, TimeSpan.FromMinutes(10));
            decision.Allowed.Should().BeTrue($"attempt {i + 1} of 3 is within the limit");
            decision.RetryAfterSeconds.Should().Be(0);
        }
    }

    [Fact]
    public async Task TryConsume_OverLimit_BlocksWithRetryAfter()
    {
        for (var i = 0; i < 3; i++)
            await _store.TryConsumeAsync("k", 3, TimeSpan.FromMinutes(10));

        var decision = await _store.TryConsumeAsync("k", 3, TimeSpan.FromMinutes(10));

        decision.Allowed.Should().BeFalse("4th request exceeds limit of 3");
        decision.RetryAfterSeconds.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(600);
    }

    [Fact]
    public async Task TryConsume_DifferentKeys_AreIsolated()
    {
        await _store.TryConsumeAsync("a", 1, TimeSpan.FromMinutes(10));
        var blockedA = await _store.TryConsumeAsync("a", 1, TimeSpan.FromMinutes(10));
        var freshB = await _store.TryConsumeAsync("b", 1, TimeSpan.FromMinutes(10));

        blockedA.Allowed.Should().BeFalse();
        freshB.Allowed.Should().BeTrue("counter for key 'b' is independent of key 'a'");
    }

    [Fact]
    public async Task TryConsume_AfterWindowExpiry_ResetsCounter()
    {
        var window = TimeSpan.FromMilliseconds(500);
        await _store.TryConsumeAsync("k", 1, window);
        (await _store.TryConsumeAsync("k", 1, window)).Allowed.Should().BeFalse();

        await Task.Delay(1000);

        var decision = await _store.TryConsumeAsync("k", 1, window);
        decision.Allowed.Should().BeTrue("window elapsed, counter must reset");
    }

    [Fact]
    public async Task TryConsume_ConcurrentCalls_NeverExceedLimit()
    {
        const int limit = 5;
        const int attempts = 40;

        var results = await Task.WhenAll(Enumerable.Range(0, attempts)
            .Select(_ => Task.Run(async () =>
                (await _store.TryConsumeAsync("k", limit, TimeSpan.FromMinutes(10))).Allowed)));

        results.Count(allowed => allowed).Should().Be(limit,
            "increment must be atomic — the old read-modify-write pattern let concurrent requests slip past the limit");
    }

    public void Dispose() => _cache.Dispose();
}
