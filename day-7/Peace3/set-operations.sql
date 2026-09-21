SELECT Author FROM dbo.Quotes
EXCEPT
SELECT q.Author
FROM dbo.Quotes q
JOIN dbo.QuoteTag qt ON qt.QuotesId = q.Id;

SELECT q.Author
FROM dbo.Quotes q
JOIN dbo.QuoteTag qt ON qt.QuotesId = q.Id
JOIN dbo.Tags t ON t.Id = qt.TagsId
WHERE t.Name = 'classic'
INTERSECT
SELECT q.Author
FROM dbo.Quotes q
JOIN dbo.QuoteTag qt ON qt.QuotesId = q.Id
JOIN dbo.Tags t ON t.Id = qt.TagsId
WHERE t.Name = 'modern';

SELECT t.Name
FROM dbo.Tags t
JOIN dbo.QuoteTag qt ON qt.TagsId = t.Id
JOIN dbo.QuoteTag qtClassic ON qtClassic.QuotesId = qt.QuotesId
JOIN dbo.Tags tc ON tc.Id = qtClassic.TagsId AND tc.Name = 'classic'
WHERE t.Name NOT IN ('classic', 'modern')
UNION
SELECT t.Name
FROM dbo.Tags t
JOIN dbo.QuoteTag qt ON qt.TagsId = t.Id
JOIN dbo.QuoteTag qtModern ON qtModern.QuotesId = qt.QuotesId
JOIN dbo.Tags tm ON tm.Id = qtModern.TagsId AND tm.Name = 'modern'
WHERE t.Name NOT IN ('classic', 'modern');