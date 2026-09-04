using MassTransit;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Messaging.Inbox;

/// <summary>
/// Consume-pipeline filter that makes every consumer idempotent: a message id
/// already processed by this endpoint is acknowledged without running the
/// consumer again. Required because the outbox/CDC publisher delivers
/// at-least-once (see docs/adr/0001-outbox-cdc-delivery-guarantees.md).
/// </summary>
public sealed class IdempotentConsumeFilter<TMessage>(
    IInboxStore inboxStore,
    ILogger<IdempotentConsumeFilter<TMessage>> logger) : IFilter<ConsumeContext<TMessage>>
    where TMessage : class
{
    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        if (context.MessageId is not { } messageId)
        {
            throw new InvalidOperationException(
                $"Message of type {typeof(TMessage).Name} arrived without a MessageId; " +
                "it cannot be deduplicated. Publishers must set the integration event id as the message id.");
        }

        var consumerName = context.ReceiveContext.InputAddress.AbsolutePath.Trim('/');

        if (!await inboxStore.TryBeginAsync(messageId, consumerName, context.CancellationToken))
        {
            logger.LogInformation(
                "Skipping duplicate delivery of {MessageId} ({MessageType}) on {Consumer}",
                messageId, typeof(TMessage).Name, consumerName);
            return;
        }

        await next.Send(context);

        await inboxStore.CompleteAsync(messageId, consumerName, context.CancellationToken);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("idempotent-consumer");
}
