using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuotesHub.Modules.Quotes.Application;
using QuotesHub.Modules.Quotes.Domain;

namespace QuotesHub.Modules.Quotes.Infrastructure;

// This module's own DbContext — no other module may reference it, and it
// has no tables belonging to any other module. That boundary is what makes
// "modular" real rather than just a folder-naming convention.
public class QuotesDbContext(DbContextOptions<QuotesDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quote>(quote =>
        {
            quote.HasKey(q => q.Id);
            quote.Property(q => q.Id)
                .HasConversion(id => id.Value, value => new QuoteId(value));

            quote.Property(q => q.Author).HasMaxLength(Quote.MaxAuthorLength).IsRequired();
            quote.Property(q => q.Text).HasMaxLength(Quote.MaxTextLength).IsRequired();
            quote.Property(q => q.CreatedAt).IsRequired();

            quote.Property(q => q.AuthoredBy)
                .HasConversion(a => a.Value, value => new AuthorId(value))
                .HasColumnName("AuthoredBy");

            quote.Property<List<Tag>>("_tags")
                .HasField("_tags")
                .HasConversion(
                    tags => string.Join(',', tags.Select(t => t.Value)),
                    csv => string.IsNullOrEmpty(csv)
                        ? new List<Tag>()
                        : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(v => new Tag(v)).ToList())
                .HasColumnName("Tags")
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<Tag>>(
                    (a, b) => a!.SequenceEqual(b!),
                    t => t.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag.GetHashCode())),
                    t => t.ToList()));
        });

        modelBuilder.Entity<OutboxMessage>().HasKey(m => m.Id);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Same transaction, same SaveChanges call: any domain event raised
        // on a tracked Quote becomes an outbox row before anything commits.
        foreach (var entry in ChangeTracker.Entries<Quote>().ToList())
        {
            foreach (var domainEvent in entry.Entity.DomainEvents)
            {
                OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = domainEvent.GetType().Name,
                    Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredAt = domainEvent.OccurredAt
                });
            }

            entry.Entity.ClearDomainEvents();
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);
}
