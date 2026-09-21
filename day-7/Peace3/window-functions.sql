SELECT
    Id,
    Author,
    Text,
    CreatedAt,
    ROW_NUMBER() OVER (PARTITION BY Author ORDER BY CreatedAt) AS RunningCount,
    DATEDIFF(
        day,
        LAG(CreatedAt) OVER (PARTITION BY Author ORDER BY CreatedAt),
        CreatedAt
    ) AS DaysSincePrevious
FROM dbo.Quotes
ORDER BY Author, CreatedAt;