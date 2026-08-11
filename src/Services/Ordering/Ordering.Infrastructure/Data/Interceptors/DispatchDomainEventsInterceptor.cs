using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ordering.Infrastructure.Data.Interceptors;

public class DispatchDomainEventsInterceptor(IMediator mediator) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        DispatchDomainEvents(eventData.Context).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await DispatchDomainEvents(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public async Task DispatchDomainEvents(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var aggregates = context.ChangeTracker
            .Entries<IAggregate>()
            .Where(entry => entry.Entity.DomainEvents.Any())
            .Select(entry => entry.Entity);

        var domainEvents = aggregates
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToList();

        aggregates.ToList().ForEach(aggregate => aggregate.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            // NOTE: External side effects should be published as integration events through a transactional outbox.
            await mediator.Publish(domainEvent);
        }
    }
}
