# Day 22 — Resilience with Polly

## Objective
Wrap an outbound dependency with Polly: retry-with-backoff (idempotent only), a circuit breaker, a timeout, and a bulkhead. Prove the circuit opens under sustained failure and recovers.

Dependency wrapped: a small controllable endpoint inside `QuotesApi` itself (`Resilience/FlakyDependencyState.cs`), toggled via `POST /test/flaky/fail` / `/recover`. Chosen over the existing `httpbin.org`-based client so the breaker-opens-then-recovers proof is fully deterministic — no dependency on a third party's uptime.

---

## 1. The Resilience Pipeline

`Program.cs`:

```csharp
resilience.AddConcurrencyLimiter(permitLimit: 2, queueLimit: 2);

resilience.AddRetry(new HttpRetryStrategyOptions
{
    ShouldHandle = args =>
    {
        var isIdempotentMethod = args.Outcome.Result?.RequestMessage?.Method is HttpMethod method &&
            (method == HttpMethod.Get || method == HttpMethod.Head ||
             method == HttpMethod.Put || method == HttpMethod.Delete ||
             method == HttpMethod.Options);

        return ValueTask.FromResult(isIdempotentMethod && HttpClientResiliencePredicates.IsTransient(args.Outcome));
    },
    MaxRetryAttempts = 4,
    BackoffType = DelayBackoffType.Exponential,
    UseJitter = true,
    Delay = TimeSpan.FromMilliseconds(200),
    OnRetry = args => { /* logs attempt number, delay, method, uri */ return default; }
});

resilience.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
{
    FailureRatio = 0.5,
    SamplingDuration = TimeSpan.FromSeconds(10),
    MinimumThroughput = 4,
    BreakDuration = TimeSpan.FromSeconds(5),
    OnOpened = args => { /* logs OPENED */ return default; },
    OnHalfOpened = args => { /* logs HALF-OPEN */ return default; },
    OnClosed = args => { /* logs CLOSED */ return default; }
});

resilience.AddTimeout(TimeSpan.FromSeconds(2));
```

**Order — bulkhead > retry > circuit breaker > timeout (outermost to innermost):** the bulkhead is admission control, deciding whether a call is even attempted before anything else spends time on it. Retry wraps the circuit breaker, so each individual retry attempt is itself subject to the breaker — once the breaker opens, further retry attempts fail fast against it instead of hitting the network. Timeout is innermost, bounding each individual attempt.

**Idempotent-only retry:** `ShouldHandle` checks the original request's HTTP method before ever considering a retry — GET/HEAD/PUT/DELETE/OPTIONS are eligible, POST is not, regardless of the failure.

---

## 2. Proof: Retry Skips Non-Idempotent Calls

Forced the dependency to fail, then called through a `POST` (not idempotent):

```
[caller] POST /api/flaky-dependency → 503
Execution attempt. Source: '.../Retry', Result: '503', Handled: 'False', Attempt: '0'
```

`Handled: 'False'`, exactly one attempt — Polly's own diagnostics confirm the retry strategy looked at this failure and declined to retry it, because the request was a POST.

---

## 3. Proof: Circuit Breaker Opens, Then Recovers

Same forced failure, this time called through `GET` (idempotent — eligible for retry):

```
[retry] Attempt 1 after 96.3198ms for GET http://localhost:5299/api/flaky-dependency
[retry] Attempt 2 after 194.367ms for GET http://localhost:5299/api/flaky-dependency
[retry] Attempt 3 after 814.2639ms for GET http://localhost:5299/api/flaky-dependency
[circuit-breaker] OPENED — failing fast for 5s
[retry] Attempt 4 after 1097.4534ms for GET http://localhost:5299/api/flaky-dependency
[caller] Circuit is open — failed fast, no call attempted
[caller] Circuit is open — failed fast, no call attempted
[caller] Circuit is open — failed fast, no call attempted
```

Exponential backoff with jitter is visible in the retry delays (96ms → 194ms → 814ms → 1097ms). Enough failing attempts land inside the 10s sampling window to cross `FailureRatio: 0.5` at `MinimumThroughput: 4`, and the breaker opens — every subsequent call fails immediately with no network attempt at all, which is the entire point of a breaker: stop hammering a dependency that's already down.

Fixed the dependency (`POST /test/flaky/recover`), waited past `BreakDuration` (5s), then made one more call:

```
[circuit-breaker] HALF-OPEN — probing with the next call
[circuit-breaker] CLOSED — recovered
```

Full lifecycle: **closed → open (sustained failure) → half-open (probe after break duration) → closed (recovered)** — all from real timestamped logs, not asserted.

---

## 4. Proof: Bulkhead Rejects Excess Concurrency

`permitLimit: 2, queueLimit: 2` — at most 4 callers admitted at once (2 executing, 2 queued), everything beyond that rejected outright. Fired 8 truly concurrent requests (dependency healthy, 300ms simulated latency so they genuinely overlap):

```
req1:200  req2:200  req3:200  req4:200
req5:429  req6:429  req7:429  req8:429
```

Exactly 4 succeeded, exactly 4 rejected — matching `permitLimit + queueLimit` precisely. Logged via Polly's own diagnostics:

```
Resilience event occurred. EventName: 'OnRateLimiterRejected', Source: '.../RateLimiter'
[caller] Bulkhead full — request rejected before any attempt
```

The rejected requests never reached the retry/breaker/timeout layers at all — the bulkhead is the outermost gate, exactly as designed.
