using Microsoft.EntityFrameworkCore;
using QuotesApi.Modules.Quotes.Models;
using QuotesApi.Modules.Quotes.Outbox;

namespace QuotesApi.Modules.Quotes.Data;

// This module's own database — no table here belongs to any other module,
// and no other module's DbContext may reference these tables. That's what
// makes the module boundary real instead of just a folder name.
public class QuotesDbContext : DbContext
{
    public QuotesDbContext(DbContextOptions<QuotesDbContext> options)
        : base(options)
    {
    }

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quote>()
            .HasIndex(q => q.Author);

        modelBuilder.Entity<OutboxMessage>()
            .HasKey(m => m.Id);
    }
}
