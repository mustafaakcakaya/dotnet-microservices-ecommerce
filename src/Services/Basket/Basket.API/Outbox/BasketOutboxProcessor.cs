using Microsoft.Extensions.Options;

namespace Basket.API.Outbox;

public sealed record BasketOutboxResult(int Published, int Failed, int Poisoned);

/// <summary>
/// One relay cycle: publish the checkout events that are due, oldest first.
///
/// Delivery is at-least-once. A message is deleted only after it was published,
/// so a crash in between republishes it with the same MessageId, and the
/// consumer's inbox drops the duplicate. Deleting instead of marking it as sent
/// is deliberate: the event carries payment details, and nothing is gained by
/// keeping them at rest once the event is out.
///
/// No publish timeout is applied. While the broker is down MassTransit waits for
/// the connection rather than failing, so an outage stalls the relay instead of
/// burning through retry attempts - a timeout would poison perfectly good
/// messages after a few minutes of downtime. Attempts and poisoning are for
/// messages that fail on their own account.
/// </summary>
public sealed class BasketOutboxProcessor(
    IDocumentStore store,
    ICheckoutEventPublisher publisher,
    IOptions<BasketOutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<BasketOutboxProcessor> logger)
{
    private const int MaxErrorLength = 2000;

    public async Task<BasketOutboxResult> ProcessOnceAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var nowUtc = timeProvider.GetUtcNow();

        await using var session = store.LightweightSession();

        var due = await session.Query<BasketOutboxMessage>()
            .Where(message => message.PoisonedOnUtc == null && message.NextAttemptOnUtc <= nowUtc)
            .OrderBy(message => message.OccurredOnUtc)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);

        int published = 0, failed = 0, poisoned = 0;

        foreach (var message in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await publisher.PublishAsync(message.Message, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (await RecordFailureAsync(session, message, exception, nowUtc, cancellationToken))
                {
                    poisoned++;
                }
                else
                {
                    failed++;
                }

                // One message failing on its own must not hold up the others.
                continue;
            }

            session.Delete(message);
            await session.SaveChangesAsync(cancellationToken);
            published++;

            logger.LogInformation(
                "Published checkout {MessageId} for {UserName}",
                message.Id, message.Message.UserName);
        }

        return new BasketOutboxResult(published, failed, poisoned);
    }

    /// <summary>Returns true when the message is now poisoned.</summary>
    private async Task<bool> RecordFailureAsync(
        IDocumentSession session,
        BasketOutboxMessage message,
        Exception exception,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        message.Attempts++;
        message.LastError = exception.Message.Length > MaxErrorLength
            ? exception.Message[..MaxErrorLength]
            : exception.Message;

        var isPoisoned = message.Attempts >= settings.MaxPublishAttempts;
        if (isPoisoned)
        {
            message.PoisonedOnUtc = nowUtc;
            logger.LogError(exception,
                "Checkout {MessageId} poisoned after {Attempts} attempts; it stays in the outbox for inspection",
                message.Id, message.Attempts);
        }
        else
        {
            message.NextAttemptOnUtc = nowUtc + Backoff(message.Attempts);
            logger.LogWarning(exception,
                "Publishing checkout {MessageId} failed, attempt {Attempts}/{MaxAttempts}; retrying at {NextAttemptOnUtc:O}",
                message.Id, message.Attempts, settings.MaxPublishAttempts, message.NextAttemptOnUtc);
        }

        session.Store(message);
        await session.SaveChangesAsync(cancellationToken);

        return isPoisoned;
    }

    private TimeSpan Backoff(int attempts)
    {
        var settings = options.Value;
        var exponent = Math.Min(attempts - 1, 16);
        var delay = settings.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, exponent);

        return TimeSpan.FromMilliseconds(Math.Min(delay, settings.RetryMaxDelay.TotalMilliseconds));
    }
}
