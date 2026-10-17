# C4 Level 3 — Component: Cache & Rate-Limit Provider Switch

Scope: `Balsm.Infrastructure` cross-cutting plumbing. Introduces `IRateLimitStore` so the
OTP rate limiter (FR-045a/b/c) is atomic and shared across replicas in Cloud (Redis) while
Standalone keeps a zero-dependency in-process store. Apple JWKS caching moves to
`HybridCache` (L1 in-memory always; L2 Redis when configured). Selection key:
`Redis:ConnectionString` — present ⇒ Redis-backed, absent ⇒ in-memory (Standalone never sets it).

Deliberately **not** migrated (per-node state is correct for them):

- `StatusHealthJob` / `StatusController` — each pod reports its own DB liveness (`IMemoryCache`).
- Supervisor `RateLimitMiddleware` — Supervisor is Standalone-only, never multi-node.

```mermaid
C4Component
    title Cache & Rate-Limit Provider Switch — Balsm.Infrastructure

    Container_Boundary(api, "Balsm.API (composition root)") {
        Component(di, "AddSharedInfrastructure", "DependencyInjection.cs", "Registers IRateLimitStore + HybridCache. Switches on Redis:ConnectionString presence")
    }

    Container_Boundary(authmod, "Balsm.Auth.Infrastructure") {
        Component(otpHandler, "RequestOtpHandler", "MediatR handler", "OTP request flow; enforces FR-045 limits before sending email")
    }

    Container_Boundary(infra, "Balsm.Infrastructure") {
        Component(policies, "OtpRateLimitPolicies", "class", "FR-045 tiers: 3/email/10min, 10/IP/60min, 10k/global/60min")
        Component(store, "IRateLimitStore", "interface", "TryConsumeAsync(key, limit, window) → RateLimitDecision")
        Component(memStore, "InMemoryRateLimitStore", "IMemoryCache + Interlocked", "Standalone default; atomic in-process")
        Component(redisStore, "RedisRateLimitStore", "StackExchange.Redis", "Atomic Lua INCR+PEXPIRE, single round-trip; fail-open + Error log on Redis outage")
        Component(apple, "AppleOidcValidator", "class", "Caches raw Apple JWKS JSON 6h via HybridCache (stampede-protected)")
        Component(hybrid, "HybridCache", "Microsoft.Extensions.Caching.Hybrid", "L1 in-memory always; L2 = IDistributedCache (Redis) when configured")
    }

    System_Ext(redis, "Redis", "Cloud-only. Shared counters + L2 cache. Not provisioned in Standalone")
    System_Ext(appleJwks, "Apple JWKS endpoint", "https://appleid.apple.com/auth/keys")

    Rel(otpHandler, policies, "CheckEmailAsync / CheckIpAsync / CheckGlobalAsync")
    Rel(policies, store, "TryConsumeAsync")
    Rel(di, store, "binds to InMemory (no Redis:ConnectionString) or Redis (present)")
    Rel(memStore, store, "implements")
    Rel(redisStore, store, "implements")
    Rel(redisStore, redis, "EVAL Lua: INCR+PEXPIRE", "TLS")
    Rel(apple, hybrid, "GetOrCreateAsync(apple_jwks, 6h)")
    Rel(hybrid, redis, "L2 via IDistributedCache (Cloud only)")
    Rel(apple, appleJwks, "GET on cache miss", "HTTPS")
```

Layer rules respected: everything lives in `Balsm.Infrastructure` (host plumbing), no module
project references touched; `RequestOtpHandler` (Auth.Infrastructure) already depended on
`OtpRateLimitPolicies` — only the call shape changes (sync → async).
