using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using QuotesApi.Contracts;

namespace QuotesApi.Modules.Identity.Authorization;

// Asks Quotes (via the Contracts interface, never its DbContext or entity
// directly) who owns the quote, instead of querying a table this module
// doesn't own.
public class CanModifyOwnQuoteHandler : AuthorizationHandler<CanModifyOwnQuoteRequirement, int>
{
    private readonly IQuoteOwnershipCheck _ownershipCheck;

    public CanModifyOwnQuoteHandler(IQuoteOwnershipCheck ownershipCheck)
    {
        _ownershipCheck = ownershipCheck;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CanModifyOwnQuoteRequirement requirement,
        int quoteId)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return;
        }

        var ownerUserId = await _ownershipCheck.GetOwnerUserIdAsync(quoteId, CancellationToken.None);

        if (ownerUserId == userId)
        {
            context.Succeed(requirement);
        }
    }
}
