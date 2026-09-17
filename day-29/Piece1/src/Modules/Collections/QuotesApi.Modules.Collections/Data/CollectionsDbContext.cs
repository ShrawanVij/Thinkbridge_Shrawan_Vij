using Microsoft.EntityFrameworkCore;
using QuotesApi.Modules.Collections.Models;

namespace QuotesApi.Modules.Collections.Data;

public class CollectionsDbContext : DbContext
{
    public CollectionsDbContext(DbContextOptions<CollectionsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Collection> Collections => Set<Collection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Collection>()
            .OwnsMany(c => c.Items, builder =>
            {
                builder.HasKey("CollectionId", nameof(CollectionItem.QuoteId));

                builder.Property(x => x.QuoteId)
                    .ValueGeneratedNever();
            });
    }
}
