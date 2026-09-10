using System.Globalization;
using Dapper;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Features.Quotes;

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
