# Day 21 — HybridCache + Stampede Protection

## Objective
Add HybridCache (in-memory + Redis) to a hot read, with stampede protection so a cache miss doesn't fan out N identical DB hits. Measure the hit rate and the DB load drop under concurrent load.

Hot read chosen: `GET /api/quotes/{id}` — added a cached counterpart (`/api/quotes/{id}/cached`) using the same repository call, so the before/after comparison is apples-to-apples.

---

## 1. The Cache Wiring

`Program.cs`:

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
});
builder.Services.AddHybridCache();
builder.Services.AddSingleton<DbHitCounter>();
```

`Extentions/QuoteApiExtensions.cs` — the cached endpoint:

```csharp
app.MapGet("/api/quotes/{id:int}/cached", async (
    int id,
    HybridCache cache,
    IQuoteRepository repository,
    DbHitCounter hitCounter,
    CancellationToken cancellationToken) =>
{
    var quote = await cache.GetOrCreateAsync(
        $"quote:{id}",
        async ct =>
        {
            hitCounter.Increment();
            await Task.Delay(200, ct); // simulate realistic DB latency
            return await repository.GetByIdAsync(id, ct);
        },
        cancellationToken: cancellationToken);

    return quote is null ? Results.NotFound() : Results.Ok(quote);
});
```

`DbHitCounter` is instrumentation only — an `Interlocked`-based counter inside the factory delegate, so it tracks how many times the *actual* data fetch ran, independent of how many HTTP requests came in. That's what makes stampede protection measurable rather than just asserted.

HybridCache's `GetOrCreateAsync` is doing two things at once: L1 (in-process memory) + L2 (Redis, via the registered `IDistributedCache`) as a two-tier cache, **and** stampede protection — concurrent callers who all miss on the same key share one in-flight factory call instead of each running it themselves.

---

## 2. Load Test: Before vs After

Same machine, same 50-connection/10s `bombardier` run, only the endpoint changes.

**Before (uncached, every request hits the DB):**
```
Reqs/sec      3262.90     876.80    5809.21
Latency       15.40ms     4.53ms   109.26ms
  50%    14.44ms   75%    18.02ms   90%    22.61ms   95%    25.87ms   99%    33.13ms
HTTP codes: 2xx - 32467
```

**After (cached, starting from a cold cache):**
```
Reqs/sec     44609.23    8021.99   61100.70
Latency        1.12ms   739.45us   241.14ms
  50%     1.01ms   75%     1.23ms   90%     1.62ms   95%     2.26ms   99%     4.06ms
HTTP codes: 2xx - 445781
```

**p99 latency: 33.13ms → 4.06ms. Throughput: ~3,263 req/s → ~44,609 req/s** (~13.7x), on identical hardware, identical concurrency, identical underlying "database."

---

## 3. Stampede Protection Under Concurrency

The "after" run above didn't start from a warm cache — the key was evicted and the DB-hit counter reset immediately before it (`POST /cache/quotes/1/evict`, `POST /cache/db-hits/reset`). So all 50 connections opened against a **cold** cache simultaneously: the classic stampede scenario, 50 concurrent callers all missing on the same key at once.

Checked the actual DB-hit count after that burst of 445,781 requests:

```
GET /cache/db-hits → {"dbHits":1}
```

**One.** Not 50, not one-per-connection — despite 50 concurrent connections racing to read a key that didn't exist yet, only one of them actually ran the factory (the simulated 200ms DB call); every other concurrent caller — and every subsequent request for the rest of the 10-second run — was served from the cache. Confirmed the value actually lives in Redis, not just process memory:

```
docker exec quotesapi-redis redis-cli KEYS "*" → quote:1
```

This is what "hit rate" and "DB load drop" mean concretely here: hit rate for that run was 445,780 / 445,781 (effectively 100% after the single unavoidable first miss), and DB load dropped from one query per request to one query total for the entire test.
