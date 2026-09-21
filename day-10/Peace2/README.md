### Exercise: Query translation and projections

#### Setup

Working against the seeded `Quotes` table (10,000 rows, 11 authors) via `QuoteDbContext`, with `LogTo` wired up in the test's `DbContextOptionsBuilder` so every generated SQL statement gets captured into a list I can inspect afterward.

#### 1. What the "whole entity" query actually costs

I started with the obvious version:

```csharp
var quotes = ctx.Quotes.Where(q => q.Author == "Mark Twain").ToList();
```

Logged SQL:
```sql
SELECT "q"."Id", "q"."Author", "q"."CreatedAt", "q"."Text", "q"."UserId"
FROM "Quotes" AS "q"
WHERE "q"."Author" = 'Mark Twain'
```

910 rows back, all 5 columns on every one, even though nothing downstream needed `UserId` or `CreatedAt`.

#### 2. Rewriting it as a projection

```csharp
var quotes = ctx.Quotes
    .Where(q => q.Author == "Mark Twain")
    .Select(q => new QuoteSummaryDto(q.Id, q.Author, q.Text))
    .ToList();
```

Logged SQL:
```sql
SELECT "q"."Id", "q"."Author", "q"."Text"
FROM "Quotes" AS "q"
WHERE "q"."Author" = 'Mark Twain'
```

Same 910 rows, but `CreatedAt` and `UserId` never leave the database. The `.Select()` isn't just shaping the C# result - it's telling EF exactly which columns to ask for.

#### 3. The client-side evaluation I got wrong first

My first instinct was to write it like this - completely reasonable-looking C#:

```csharp
var quotes = ctx.Quotes.ToList().Where(q => q.Author == "Mark Twain").ToList();
```

Logged SQL - this is the part that surprised me:
```sql
SELECT "q"."Id", "q"."Author", "q"."CreatedAt", "q"."Text", "q"."UserId"
FROM "Quotes" AS "q"
```

No `WHERE` clause at all. The first `.ToList()` executes immediately, pulling all 10,000 rows into a plain C# `List<Quote>`. Everything after that - the `.Where()` - is just LINQ-to-Objects filtering an in-memory list, not a database query. Same 910 rows came out the other end, so it *looked* correct, which is exactly what makes this bug dangerous.

The fix was just reordering:
```csharp
var quotes = ctx.Quotes.Where(q => q.Author == "Mark Twain").ToList();
```
Now `.Where()` runs while the query is still `IQueryable`, so it gets translated into SQL before anything is fetched.

#### Results

| Query | Rows | Columns fetched | Where clause in SQL? |
|---|---|---|---|
| Whole entity | 910 | 5 | yes |
| Projected (`.Select`) | 910 | 3 | yes |
| `.ToList().Where()` (bug) | 910 | 5 | no - filtered client-side |
| Fixed | 910 | 5 | yes |

#### What did you learn this session?

That `.ToList()` is a hard boundary - everything before it is a query EF Core can optimize and translate; everything after it is just regular C# working on data already sitting in memory, and there's no warning when you accidentally cross that line.

#### What would break this?

If someone "fixes" a similar bug by moving `.ToList()` to the very end without checking *where* in the chain it went - e.g. `ctx.Quotes.Select(ComplexClientMethod).ToList()` - the projection itself might still force client-side evaluation if it calls something EF can't translate, so the `WHERE`/`SELECT` looking right in the code doesn't guarantee it's right in the generated SQL; you still have to check the log.