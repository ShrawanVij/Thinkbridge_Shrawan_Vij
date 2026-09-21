namespace QuotesHub.SharedKernel;

// The only thing allowed to cross a module boundary: published to the
// outbox by the owning module, consumed by other modules over the message
// broker (never via a direct in-process reference or shared database table).
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
