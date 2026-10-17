# C4 Level 4 — Dynamic: OTP Rate-Limit Check (Cloud, Redis-backed)

Flow for `POST /auth/otp/request` (FR-045). Cloud runs N replicas; counters must be shared
and atomic — a plain get/set cache would let concurrent requests on different pods slip past
the limit (read-modify-write race). Redis Lua script performs INCR + PEXPIRE + limit check
in one atomic server-side step.

**Trust boundary & auth:** Redis is inside the Cloud VPC (Railway private network),
reached with connection-string auth over TLS. Rate-limit keys carry no PHI —
`otp:email:{email}` contains the account email (non-PHI cloud data per P001) with a
bounded TTL; nothing else is stored.

**Failure mode — fail-open:** if Redis is unreachable, `RedisRateLimitStore` logs `Error`
and **allows** the request. Rationale: OTP send is defense-in-depth (account lockout still
guards verification); blocking every login on a Redis blip is worse for a healthcare app
than briefly losing spray protection. `AbortOnConnectFail=false` so the multiplexer
reconnects in the background.

```mermaid
C4Dynamic
    title OTP request rate-limit path — Cloud mode

    Container_Boundary(pod, "Balsm.API pod (replica 1..N)") {
        Component(ctrl, "AuthController", "POST /auth/otp/request")
        Component(mediatr, "MediatR", "RequestOtpCommand")
        Component(handler, "RequestOtpHandler", "Balsm.Auth.Infrastructure")
        Component(policies, "OtpRateLimitPolicies", "Balsm.Infrastructure")
        Component(store, "RedisRateLimitStore : IRateLimitStore", "Balsm.Infrastructure")
    }
    System_Ext(redis, "Redis (shared, VPC)", "Counters visible to every replica")
    System_Ext(resend, "Resend", "OTP email sub-processor")

    Rel(ctrl, mediatr, "1. Send(RequestOtpCommand)")
    Rel(mediatr, handler, "2. Handle(cmd, ct)")
    Rel(handler, policies, "3. CheckEmailAsync → CheckIpAsync → CheckGlobalAsync")
    Rel(policies, store, "4. TryConsumeAsync(otp:email:{email}, 3, 10min)")
    Rel(store, redis, "5. EVAL: c=INCR(key); if c==1 PEXPIRE; if c>limit return PTTL else 0")
    Rel(store, policies, "6. RateLimitDecision(Allowed, RetryAfterSeconds)")
    Rel(handler, resend, "7. Send OTP email (only if all three tiers allow)")
```

Rejected tier ⇒ `OtpRateLimitException(retryAfter, tier)` ⇒ HTTP 429 + `Retry-After`
(unchanged contract — no endpoint, DTO, or Insomnia/OpenAPI change in this refactor).

Standalone mode: identical sequence with `InMemoryRateLimitStore` at step 4–6
(Interlocked increment on an `IMemoryCache` entry — atomic in-process, no network).
