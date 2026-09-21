;WITH AuthorStats AS (
    SELECT
        Author,
        COUNT(*)       AS QuoteCount,
        MAX(CreatedAt) AS LatestCreatedAt
    FROM dbo.Quotes
    GROUP BY Author
)
SELECT TOP (10)
    q.Id,
    s.Author,
    s.QuoteCount,
    q.Text AS MostRecentQuote,
    q.UserId,
    q.CreatedAt
FROM AuthorStats AS s
JOIN dbo.Quotes AS q
    ON q.Author    = s.Author
   AND q.CreatedAt = s.LatestCreatedAt
ORDER BY s.QuoteCount DESC, s.Author;