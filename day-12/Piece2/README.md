# Day 12 — When to Reach for Dapper

## Objective
EF is the default. Reimplement the fastest-needed read query (the Day 12 quote feed) with Dapper, compare the SQL and timing against the EF version, and write the rule for when to actually drop to Dapper.

---

## 1. The EF implementation

`Features/Quotes/GetQuoteFeedQuery.cs` (unchanged from Piece1):
```csharp
public record GetQuoteFeedQuery(int Page, int Size) : IRequest<List<QuoteFeedItem>>;

public class GetQuoteFeedQueryHandler(QuoteDbContext db) : IRequestHandler<GetQuoteFeedQuery, List<QuoteFeedItem>>
{
    public async Task<List<QuoteFeedItem>> Handle(GetQuoteFeedQuery request, CancellationToken cancellationToken)
    {
        return await db.Quotes
            .OrderByDescending(q => q.CreatedAt)
            .Skip((request.Page - 1) * request.Size)
            .Take(request.Size)
            .Select(q => new QuoteFeedItem(
                q.Id,
                q.Author,
                q.Text,
                q.CreatedAt,
                string.Join(", ", q.Tags.Select(t => t.Name))))
            .ToListAsync(cancellationToken);
    }
}
```

---

## 2. The Dapper implementation

`Features/Quotes/GetQuoteFeedDapperQuery.cs`:
```csharp
public record GetQuoteFeedDapperQuery(int Page, int Size) : IRequest<List<QuoteFeedItem>>;

public class GetQuoteFeedDapperQueryHandler(QuoteDbContext db) : IRequestHandler<GetQuoteFeedDapperQuery, List<QuoteFeedItem>>
{
    private record QuoteFeedRow(long Id, string Author, string Text, string CreatedAt, string Tags);

    private const string Sql = """
        SELECT
            q."Id" AS Id,
            q."Author" AS Author,
            q."Text" AS Text,
            q."CreatedAt" AS CreatedAt,
            COALESCE((
                SELECT GROUP_CONCAT(t."Name", ', ')
                FROM "QuoteTag" qt
                JOIN "Tags" t ON t."Id" = qt."TagsId"
                WHERE qt."QuotesId" = q."Id"
            ), '') AS Tags
        FROM "Quotes" q
        ORDER BY q."CreatedAt" DESC
        LIMIT @Size OFFSET @Offset
        """;

    public async Task<List<QuoteFeedItem>> Handle(GetQuoteFeedDapperQuery request, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(db.Database.GetConnectionString());

        var rows = await connection.QueryAsync<QuoteFeedRow>(
            Sql,
            new { request.Size, Offset = (request.Page - 1) * request.Size });

        return rows
            .Select(r => new QuoteFeedItem(
                (int)r.Id,
                r.Author,
                r.Text,
                DateTime.Parse(r.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                r.Tags))
            .ToList();
    }
}
```

Uses its own `SqliteConnection` (via the same connection string EF uses) rather than going through `QuoteDbContext` at all — no change tracker, no LINQ translation, just SQL in and rows out.

---

## 3. SQL comparison

**EF** (captured live from `sql.log`):
```sql
SELECT "q1"."Id", "q1"."Author", "q1"."Text", "q1"."CreatedAt", "s"."Name", "s"."QuotesId", "s"."TagsId", "s"."Id"
FROM (
    SELECT "q"."Id", "q"."Author", "q"."Text", "q"."CreatedAt"
    FROM "Quotes" AS "q"
    ORDER BY "q"."CreatedAt" DESC
    LIMIT @p1 OFFSET @p
) AS "q1"
LEFT JOIN (
    SELECT "t"."Name", "q0"."QuotesId", "q0"."TagsId", "t"."Id"
    FROM "QuoteTag" AS "q0"
    INNER JOIN "Tags" AS "t" ON "q0"."TagsId" = "t"."Id"
) AS "s" ON "q1"."Id" = "s"."QuotesId"
ORDER BY "q1"."CreatedAt" DESC, "q1"."Id", "s"."QuotesId", "s"."TagsId"
```
EF doesn't flatten tags in SQL — it fetches one row per quote-tag pair (a fan-out join), then reassembles them into one `QuoteFeedItem` and runs `string.Join` in C# after the data comes back.

**Dapper** (the hand-written query above): one correlated `GROUP_CONCAT` subquery per row, so the flattening happens inside SQLite — no fan-out, no client-side regrouping.

---

## 4. Timing comparison

50 iterations each, page 1, size 20, same seeded `quotes.db`, measured with `Stopwatch` in `QuotesApi.Tests/FeedQueryBenchmarkTests.cs`:

```
EF:     avg 4.037 ms, median 3.731 ms, min 3.405 ms, max 10.449 ms
Dapper: avg 5.092 ms, median 4.756 ms, min 4.420 ms, max 15.418 ms
```

EF was faster here, not Dapper. Two likely reasons: this Dapper handler opens a brand-new `SqliteConnection` per call instead of reusing EF's pooled one, and at only 20 rows the query is cheap enough that EF's own translation overhead was already negligible — there wasn't much left for Dapper to strip out.

---

## Rule: Dapper vs EF

Stay on EF by default, and only reach for Dapper when profiling shows EF's own overhead — change tracking, LINQ translation, or a shape EF can't express well (like the fan-out join above) — is actually the bottleneck, then confirm the switch with a real before/after measurement rather than assuming it; Dapper isn't automatically faster, and in this exact case it measurably lost to EF.
