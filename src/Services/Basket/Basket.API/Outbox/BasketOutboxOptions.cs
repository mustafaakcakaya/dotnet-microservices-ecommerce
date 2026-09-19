namespace Basket.API.Outbox;

public class BasketOutboxOptions
{
    public const string SectionName = "BasketOutbox";

    /// <summary>Maximum number of messages published per cycle.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Delay between cycles.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>After this many failed publishes a message is poisoned and skipped.</summary>
    public int MaxPublishAttempts { get; set; } = 10;

    /// <summary>Base delay for the per-message exponential backoff.</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Upper bound for the backoff delay.</summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(1);
}
