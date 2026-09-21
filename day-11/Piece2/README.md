# Day 11 — Drop p99 by 10x

## Objective
Fix the N+1 + missing-index endpoint from Piece1: eliminate the N+1 (projection), add the right index, and re-measure under the same load to confirm the improvement.

---

## 1. The Fix

**Eliminated the N+1** — `Program.cs`, replaced the 12-query loop with one grouped projection query:
```csharp
app.MapGet("/reports/authors-quotes-n1", async (QuoteDbContext db) =>
{
    var result = await db.Quotes
        .GroupBy(q => q.Author)
        .Select(g => new { author = g.Key, quoteCount = g.Count() })
        .ToListAsync();

    return Results.Ok(result);
});
```

**Added the index** — `QuoteDbContext.cs`:
```csharp
modelBuilder.Entity<Quote>()
    .HasIndex(q => q.Author);
```
Applied via migration `AddAuthorIndex`:
```sql
CREATE INDEX "IX_Quotes_Author" ON "Quotes" ("Author");
```

---

## 2. SQL: Before vs After

**Before (Piece1)** — 1 query to list authors, then 11 more, one per author:
```sql
SELECT DISTINCT "q"."Author" FROM "Quotes" AS "q"

SELECT "q"."Id", "q"."Author", "q"."CreatedAt", "q"."Text", "q"."UserId"
FROM "Quotes" AS "q"
WHERE "q"."Author" = @author
```

**After (Piece2)** — 1 query total:
```sql
SELECT "q"."Author" AS "author", COUNT(*) AS "quoteCount"
FROM "Quotes" AS "q"
GROUP BY "q"."Author"
```
12 round-trips per request down to 1.

---

## 3. Execution Plan: Before vs After

**Before:**
```
SCAN q
```
Full table scan, no index on `Author`.

**After:**
```
SCAN q USING COVERING INDEX IX_Quotes_Author
```
Still visits every row (unavoidable for a `GROUP BY` with no filter), but now reads the index only — never touches the main table.

---

## 4. Load Test: Before vs After

Same `bombardier` commands as Piece1, pointed at the fixed endpoint on port 5221:
```powershell
cd D:\thinkschool\day-11\Piece1
.\bombardier.exe -c 5 -d 10s -l http://localhost:5221/reports/authors-quotes-n1
.\bombardier.exe -c 50 -d 10s -l http://localhost:5221/reports/authors-quotes-n1
```

### Light load (5 connections, 10s)
Before (Piece1):
```
Reqs/sec        27.13
50%   188.93ms
99%   624.25ms
HTTP codes: 2xx - 252, others - 0
```
After (Piece2):
```
Reqs/sec      2042.93
50%     2.22ms
99%     5.69ms
HTTP codes: 2xx - 20422, others - 0
```

### Realistic load (50 connections, 10s)
Before (Piece1):
```
Reqs/sec        55.09
50%      1.00s
99%      1.79s
HTTP codes: 2xx - 500, others - 0
```
After (Piece2):
```
Reqs/sec      2763.55
50%    16.99ms
99%    41.02ms
HTTP codes: 2xx - 27436, others - 0
```

---

## p99 Improvement

- Light load (5 connections): 624.25ms -> 5.69ms = **109.7x** improvement.
- Realistic load (50 connections): 1.79s -> 41.02ms = **43.6x** improvement.

Both comfortably clear the 10x target. The realistic-load improvement is smaller than the light-load one because at 50 connections the fixed endpoint is now fast enough that connection/thread scheduling overhead makes up a bigger share of the remaining latency — but it's still a ~44x drop in p99, and throughput went from 55 to 2763 reqs/sec in the same test.
