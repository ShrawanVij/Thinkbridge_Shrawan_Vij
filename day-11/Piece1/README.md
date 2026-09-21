# Day 11 — Profile a Slow Endpoint

## Objective
Introduce a data-access anti-pattern into the Week-1 API on purpose — an N+1 query over authors -> quotes, with no index backing the lookup — and profile the resulting performance under load: baseline p50/p99, the SQL it actually emits, and the execution plan.

---

## 1. Baseline Profiling (p50 / p99)

Load generated with `bombardier` (portable binary, no admin rights / Chocolatey needed):

```powershell
Invoke-WebRequest -Uri "https://github.com/codesenberg/bombardier/releases/download/v1.2.6/bombardier-windows-amd64.exe" -OutFile "bombardier.exe"

.\bombardier.exe -c 5 -d 10s -l http://localhost:5220/reports/authors-quotes-n1
.\bombardier.exe -c 50 -d 10s -l http://localhost:5220/reports/authors-quotes-n1
```

### Light load (5 connections, 10s)
```
Statistics        Avg      Stdev        Max
  Reqs/sec        27.13      35.84     245.82
  Latency      199.53ms    60.05ms   635.98ms
  Latency Distribution
     50%   188.93ms
     75%   213.49ms
     90%   240.21ms
     95%   276.89ms
     99%   624.25ms
  HTTP codes:
    1xx - 0, 2xx - 252, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    17.90KB/s
```

### Realistic load (50 connections, 10s)
```
Statistics        Avg      Stdev        Max
  Reqs/sec        55.09     195.86    2001.28
  Latency         1.01s   254.31ms      3.04s
  Latency Distribution
     50%      1.00s
     75%      1.19s
     90%      1.41s
     95%      1.52s
     99%      1.79s
  HTTP codes:
    1xx - 0, 2xx - 500, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    34.66KB/s
```

Median latency goes from ~189ms to ~1.00s (about 5x worse) going from 5 to 50 concurrent connections — the cost isn't fixed, it scales with concurrent traffic on top of the per-request N+1 cost.

---

## 2. The Offending SQL (N+1 Anti-Pattern)

Captured via EF Core's `LogTo` on `RelationalEventId.CommandExecuted`, writing to `sql.log`.

**The "1" query (parent) — list of distinct authors:**
```sql
SELECT DISTINCT "q"."Author"
FROM "Quotes" AS "q"
```

**The "N" queries — one per author, 11 times:**
```sql
SELECT "q"."Id", "q"."Author", "q"."CreatedAt", "q"."Text", "q"."UserId"
FROM "Quotes" AS "q"
WHERE "q"."Author" = @author
```

One request to the endpoint results in 12 round-trips to the database instead of one.

---

## Two biggest problems found

1. **N+1 query pattern** — 1 query to list authors, then a separate round-trip per author instead of one grouped query. This is what turns a single request into 12 sequential database round-trips.
2. **Missing index on `Author`** — with no index, each of those 11 per-author queries does a full table `SCAN`, so the cost of each one grows with total table size rather than with how many rows actually match. Together with the N+1 pattern, this is why the endpoint degrades so sharply under concurrent load.
