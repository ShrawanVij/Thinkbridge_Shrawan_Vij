namespace QuotesHub.SharedKernel;

// Raised inside an aggregate as a side effect of a state change, handled
// in-process (e.g. by other aggregates in the same module, or to build the
// outbox row) — never crosses a module boundary directly.
public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
