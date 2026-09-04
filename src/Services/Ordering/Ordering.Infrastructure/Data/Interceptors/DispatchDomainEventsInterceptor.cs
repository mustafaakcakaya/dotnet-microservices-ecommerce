using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ordering.Application.Orders.IntegrationEvents;
using Ordering.Infrastructure.Outbox;

namespace Ordering.Infrastructure.Data.Interceptors;

/// <summary>
/// Runs at the start of SaveChanges, before the transaction commits:
/// 1. Maps each domain event to its integration event and stages an
///    <see cref="OutboxMessage"/> on the same context, so the aggregate and the
///    outbox row are persisted atomically in one SQL transaction.
/// 2. Publishes the domain event in-process via MediatR for internal handlers.
/// No external side effect (broker publish) happens here; the CDC worker picks
/// up committed outbox rows and publishes them to the message broker.
/// </summary>
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

        var correlationId = Activity.Current?.TraceId.ToString();

        foreach (var domainEvent in domainEvents)
        {
            var mapped = OrderIntegrationEventMapper.Map(domainEvent, correlationId);
            if (mapped is not null)
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.From(mapped.Event, mapped.AggregateId));
            }

            await mediator.Publish(domainEvent);
        }
    }
}
