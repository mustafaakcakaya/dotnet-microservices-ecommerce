namespace Ordering.Worker.Cdc;

public class OutboxCdcOptions
{
    public const string SectionName = "OutboxCdc";

    /// <summary>Checkpoint owner name; allows multiple independent consumers in theory.</summary>
    public string ConsumerName { get; set; } = "ordering-outbox-worker";

    /// <summary>Maximum number of outbox messages read from CDC per cycle.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Delay between polls when no new changes are found.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>After this many failed publish attempts a message is marked poisoned and skipped.</summary>
    public int MaxPublishAttempts { get; set; } = 10;

    /// <summary>Base delay for exponential backoff after a failed cycle.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound for the exponential backoff delay.</summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(1);
}
