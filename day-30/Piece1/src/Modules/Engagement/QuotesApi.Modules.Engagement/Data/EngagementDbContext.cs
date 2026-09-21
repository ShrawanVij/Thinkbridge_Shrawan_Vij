using Microsoft.EntityFrameworkCore;
using QuotesApi.Modules.Engagement.Models;

namespace QuotesApi.Modules.Engagement.Data;

public class EngagementDbContext : DbContext
{
    public EngagementDbContext(DbContextOptions<EngagementDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
}
