using Microsoft.AspNetCore.Authorization;

namespace QuotesApi.Modules.Identity.Authorization;

// Backs both "can-edit-own-quote" and "can-delete-own-quote" — the check is
// identical (does this quote belong to the caller), only the action differs,
// and the action itself is enforced by which endpoint required which policy.
public class CanModifyOwnQuoteRequirement : IAuthorizationRequirement
{
}
