namespace QuotesHub.Modules.Quotes.Domain;

// A reference to a user from the Identity module — by id only. The Quotes
// module never references Identity's User entity directly; that's the rule
// that keeps modules independently deployable/testable/replaceable.
public readonly record struct AuthorId(Guid Value);
