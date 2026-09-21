# Day 12 — Read Models + CQRS-lite

## Objective
Split one feature — creating and listing quotes — into a write path (normalized, validated) and a read path (denormalized, projection-shaped for the screen that displays it). No event sourcing, no separate database: just two different code paths against the same `Quotes` table, wired through MediatR.

---

## The command handler (write model)

`Features/Quotes/CreateQuoteCommand.cs`:
```csharp
public record CreateQuoteCommand(string Author, string Text, int UserId) : IRequest<CreateQuoteResult>;

public record CreateQuoteResult(int Id, string Author, string Text, int UserId, DateTime CreatedAt);

public class CreateQuoteCommandHandler(QuoteDbContext db) : IRequestHandler<CreateQuoteCommand, CreateQuoteResult>
{
    public async Task<CreateQuoteResult> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Author))
            errors["author"] = ["Author is required."];
        else if (request.Author.Length > 100)
            errors["author"] = ["Author cannot exceed 100 characters."];

        if (string.IsNullOrWhiteSpace(request.Text))
            errors["text"] = ["Text is required."];
        else if (request.Text.Length > 1000)
            errors["text"] = ["Text cannot exceed 1000 characters."];

        if (errors.Count > 0)
            throw new QuoteValidationException(errors);

        var quote = new Quote
        {
            Author = request.Author,
            Text = request.Text,
            UserId = request.UserId,
            CreatedAt = DateTime.UtcNow
        };

        db.Quotes.Add(quote);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateQuoteResult(quote.Id, quote.Author, quote.Text, quote.UserId, quote.CreatedAt);
    }
}
```

Writes against the full, normalized `Quote` entity, with validation living right where the write happens. Wired at `POST /cqrs/quotes` (auth required, `UserId` taken from the JWT claim, never trusted from the request body).

Output (valid quote):
```powershell
PS D:\thinkschool\day-12\Piece1\QuotesApi> Invoke-RestMethod -Uri "http://localhost:5220/cqrs/quotes" -Method Post -Body $quoteBody -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }

id        : 10002
author    : Ada Lovelace
text      : That brain of mine is something more than merely mortal.
userId    : 1
createdAt : 2026-08-22T05:35:31.0214784Z
```

Output (blank author/text — validation path):
```powershell
PS D:\thinkschool\day-12\Piece1\QuotesApi> Invoke-WebRequest -Uri "http://localhost:5220/cqrs/quotes" -Method Post -Body $badBody -ContentType "application/json" -Headers @{ Authorization = "Bearer $token" }

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"author":["Author is required."],"text":["Text is required."]}}
```

---

## The query / read model

`Features/Quotes/GetQuoteFeedQuery.cs`:
```csharp
public record GetQuoteFeedQuery(int Page, int Size) : IRequest<List<QuoteFeedItem>>;

public record QuoteFeedItem(int Id, string Author, string Text, DateTime CreatedAt, string Tags);

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

`QuoteFeedItem` is denormalized on purpose: `Tags` is a flat comma-joined string instead of a `List<Tag>`, because the feed screen only ever prints tags next to a quote — it never edits them. The `.Select()` projects straight into that shape in one SQL query; nothing about the normalized many-to-many `Tag` relationship leaks into the read model. Wired at `GET /cqrs/quotes/feed?page=&size=`.

Output:
```powershell
PS D:\thinkschool\day-12\Piece1\QuotesApi> Invoke-RestMethod -Uri "http://localhost:5220/cqrs/quotes/feed?page=1&size=3" -Method Get | ConvertTo-Json

{
    "value":  [
                  {
                      "id":  10002,
                      "author":  "Ada Lovelace",
                      "text":  "That brain of mine is something more than merely mortal.",
                      "createdAt":  "2026-08-22T05:35:31.0214784",
                      "tags":  ""
                  },
                  {
                      "id":  10001,
                      "author":  "Ada Lovelace",
                      "text":  "That brain of mine is something more than merely mortal.",
                      "createdAt":  "2026-08-22T04:56:10.8498234",
                      "tags":  ""
                  },
                  {
                      "id":  10000,
                      "author":  "Albert Einstein",
                      "text":  "Benchmark quote number 9999 for load testing the slow endpoint",
                      "createdAt":  "2025-01-08T07:39:00",
                      "tags":  ""
                  }
              ],
    "Count":  3
}
```
`tags` shows empty because this seeded dataset has no tag associations — the shape is still denormalized and correct, there's just nothing to flatten yet.

---

## What got simpler

The read model never has to know about validation, entity tracking, or `SaveChanges` — it's a one-way `Select` straight to the exact DTO the screen needs, so there's no risk of accidentally shipping write-side concerns (or the wrong shape) to a read-only screen.
