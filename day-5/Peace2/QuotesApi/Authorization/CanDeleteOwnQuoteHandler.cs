using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Authorization;

public class CanDeleteOwnQuoteHandler
    : AuthorizationHandler<CanDeleteOwnQuoteRequirement>
{
    private readonly QuoteDbContext _db;

    public CanDeleteOwnQuoteHandler(QuoteDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CanDeleteOwnQuoteRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(
            ClaimTypes.NameIdentifier);

        if (userIdClaim is null ||
            !int.TryParse(userIdClaim.Value, out var userId))
        {
            return;
        }

        if (context.Resource is int quoteId)
        {
            var quote = await _db.Quotes
                .FirstOrDefaultAsync(q => q.Id == quoteId);

            if (quote is not null &&
                quote.UserId == userId)
            {
                context.Succeed(requirement);
            }
        }
    }
}