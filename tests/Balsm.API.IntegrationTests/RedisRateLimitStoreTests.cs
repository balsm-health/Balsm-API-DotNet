using Balsm.Infrastructure.RateLimit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>
/// Cloud-mode rate-limit store against a real Redis (Testcontainers).
/// Verifies the Lua INCR+PEXPIRE script is atomic across concurrent callers
/// and that the store fails open when Redis is unreachable.
/// </summary>
public sealed class RedisRateLimitStoreTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder().Build();
    private ConnectionMultiplexer _mux = null!;
    private RedisRateLimitStore _store = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _mux = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        _store = new RedisRateLimitStore(_mux, NullLogger<RedisRateLimitStore>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _mux.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task TryConsume_UnderLimit_Allows()
    {
        for (var i = 0; i < 3; i++)
        {
            var decision = await _store.TryConsumeAsync("under", 3, TimeSpan.FromMinutes(10));
            decision.Allowed.Should().BeTrue($"attempt {i + 1} of 3 is within the limit");
        }
    }

    [Fact]
    public async Task TryConsume_OverLimit_BlocksWithRetryAfter()
    {
        for (var i = 0; i < 3; i++)
            await _store.TryConsumeAsync("over", 3, TimeSpan.FromMinutes(10));

        var decision = await _store.TryConsumeAsync("over", 3, TimeSpan.FromMinutes(10));

        decision.Allowed.Should().BeFalse("4th request exceeds limit of 3");
        decision.RetryAfterSeconds.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(600);
    }

    [Fact]
    public async Task TryConsume_ConcurrentCalls_NeverExceedLimit()
    {
        const int limit = 5;
        const int attempts = 40;

        var results = await Task.WhenAll(Enumerable.Range(0, attempts)
            .Select(_ => Task.Run(async () =>
                (await _store.TryConsumeAsync("concurrent", limit, TimeSpan.FromMinutes(10))).Allowed)));

        results.Count(allowed => allowed).Should().Be(limit,
            "the Lua script must make INCR + limit check atomic across concurrent callers");
    }

    [Fact]
    public async Task TryConsume_AfterWindowExpiry_ResetsCounter()
    {
        var window = TimeSpan.FromMilliseconds(500);
        await _store.TryConsumeAsync("expiry", 1, window);
        (await _store.TryConsumeAsync("expiry", 1, window)).Allowed.Should().BeFalse();

        await Task.Delay(1000);

        var decision = await _store.TryConsumeAsync("expiry", 1, window);
        decision.Allowed.Should().BeTrue("PEXPIRE elapsed, counter must reset");
    }

    [Fact]
    public async Task TryConsume_RedisUnreachable_FailsOpen()
    {
        // Multiplexer pointed at a closed port; abortConnect=false so Connect succeeds
        // and operations fail fast with RedisConnectionException.
        await using var deadMux = await ConnectionMultiplexer.ConnectAsync(
            "localhost:1,abortConnect=false,connectTimeout=200,syncTimeout=200,connectRetry=0");
        var store = new RedisRateLimitStore(deadMux, NullLogger<RedisRateLimitStore>.Instance);

        var decision = await store.TryConsumeAsync("dead", 1, TimeSpan.FromMinutes(10));

        decision.Allowed.Should().BeTrue(
            "rate limiting is defense-in-depth — a Redis outage must not block all OTP logins");
    }
}
