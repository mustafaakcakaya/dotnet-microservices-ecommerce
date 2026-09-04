using Microsoft.EntityFrameworkCore;
using Ordering.Application.Data;
using Ordering.Domain.Models;
using Ordering.Infrastructure.Inbox;
using Ordering.Infrastructure.Outbox;
using System.Reflection;

namespace Ordering.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // Written by Ordering.Worker and by consumers at runtime; declared here so
    // EF migrations own the whole OrderDb schema, CDC setup included.
    public DbSet<OutboxCdcCheckpoint> OutboxCdcCheckpoints => Set<OutboxCdcCheckpoint>();
    public DbSet<OutboxPublishFailure> OutboxPublishFailures => Set<OutboxPublishFailure>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Applies all IEntityTypeConfiguration implementations from this assembly.
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(builder);
    }
}
