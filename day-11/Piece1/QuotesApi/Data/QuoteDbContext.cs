using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;

namespace QuotesApi.Data;

public class QuoteDbContext : DbContext
{
    public QuoteDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Collection>()
            .OwnsMany(c => c.Items, builder =>
            {
                builder.HasKey("CollectionId", nameof(CollectionItem.QuoteId));

                builder.Property(x => x.QuoteId)
                    .ValueGeneratedNever();
            });
        modelBuilder.Entity<RefreshToken>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);    
    }
}