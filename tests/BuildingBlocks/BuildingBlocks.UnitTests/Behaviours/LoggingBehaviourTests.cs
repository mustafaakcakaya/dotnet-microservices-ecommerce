using BuildingBlocks.Behaviours;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.UnitTests.Behaviours;

/// <summary>
/// The pipeline used to log the whole request object. Commands such as
/// CreateOrderCommand carry payment details, so that wrote card numbers and CVVs
/// into the logs in clear text. These tests pin the behaviour down.
/// </summary>
public sealed class LoggingBehaviourTests
{
    private const string CardNumber = "4111111111111111";
    private const string Cvv = "123";

    [Fact]
    public async Task Handle_DoesNotLogRequestContents()
    {
        var logger = new RecordingLogger<LoggingBehaviour<PaymentRequest, string>>();
        var behaviour = new LoggingBehaviour<PaymentRequest, string>(logger);
        var request = new PaymentRequest(CardNumber, Cvv, "Mustafa Akcakaya");

        await behaviour.Handle(request, () => Task.FromResult("ok"), CancellationToken.None);

        var everythingLogged = string.Join("\n", logger.Messages);
        Assert.DoesNotContain(CardNumber, everythingLogged);
        Assert.DoesNotContain(Cvv, everythingLogged, StringComparison.Ordinal);
        Assert.DoesNotContain("Mustafa Akcakaya", everythingLogged);
        // Not even the type's ToString(), which records expand into their values.
        Assert.DoesNotContain(request.ToString()!, everythingLogged);
    }

    [Fact]
    public async Task Handle_LogsRequestAndResponseTypeNames()
    {
        var logger = new RecordingLogger<LoggingBehaviour<PaymentRequest, string>>();
        var behaviour = new LoggingBehaviour<PaymentRequest, string>(logger);

        await behaviour.Handle(
            new PaymentRequest(CardNumber, Cvv, "Mustafa Akcakaya"),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        var everythingLogged = string.Join("\n", logger.Messages);
        Assert.Contains(nameof(PaymentRequest), everythingLogged);
        Assert.Contains(nameof(String), everythingLogged);
    }

    [Fact]
    public async Task Handle_ReturnsHandlerResponse()
    {
        var behaviour = new LoggingBehaviour<PaymentRequest, string>(
            new RecordingLogger<LoggingBehaviour<PaymentRequest, string>>());

        var response = await behaviour.Handle(
            new PaymentRequest(CardNumber, Cvv, "Mustafa Akcakaya"),
            () => Task.FromResult("handled"),
            CancellationToken.None);

        Assert.Equal("handled", response);
    }

    public record PaymentRequest(string CardNumber, string Cvv, string CardName) : IRequest<string>;

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
