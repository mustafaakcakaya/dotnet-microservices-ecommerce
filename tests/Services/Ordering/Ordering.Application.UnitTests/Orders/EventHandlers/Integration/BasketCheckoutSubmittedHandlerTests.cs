using BuildingBlocks.Messaging.Events.Basket.V1;
using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Orders.Commands.CreateOrder;
using Ordering.Application.Orders.EventHandlers.Integration;

namespace Ordering.Application.UnitTests.Orders.EventHandlers.Integration;

public class BasketCheckoutSubmittedHandlerTests
{
    private static readonly Guid IphoneId = Guid.NewGuid();
    private static readonly Guid CaseId = Guid.NewGuid();

    [Fact]
    public async Task Consume_BuildsTheOrderFromTheCheckoutLines()
    {
        var message = Checkout();

        var command = Assert.IsType<CreateOrderCommand>(await ConsumeAsync(message));

        Assert.Equal(message.CustomerId, command.Order.CustomerId);
        Assert.Equal("Mustafa", command.Order.ShippingAddress.FirstName);
        Assert.Equal("Bahcelievler No:4", command.Order.BillingAddress.AddressLine);

        Assert.Collection(command.Order.OrderItems,
            item =>
            {
                Assert.Equal(IphoneId, item.ProductId);
                Assert.Equal(2, item.Quantity);
                Assert.Equal(500, item.Price);
            },
            item =>
            {
                Assert.Equal(CaseId, item.ProductId);
                Assert.Equal(1, item.Quantity);
                Assert.Equal(25, item.Price);
            });

        // The products travel with the command so Ordering can add any it does not know yet.
        Assert.NotNull(command.Products);
        Assert.Contains(command.Products!, product => product.ProductId == CaseId && product.Name == "Phone case");
    }

    [Fact]
    public async Task Consume_PassesOnlyTheTokenForPayment()
    {
        var command = Assert.IsType<CreateOrderCommand>(await ConsumeAsync(Checkout()));

        Assert.Equal("pt_0123456789abcdef", command.Order.Payment.PaymentToken);
        Assert.Equal("4444", command.Order.Payment.CardLast4);
        Assert.Equal(1, command.Order.Payment.PaymentMethod);
    }

    // A total that disagrees with the lines is a broken message, not something to
    // repair: no order is created and the message faults.
    [Fact]
    public async Task Consume_WhenTotalDoesNotMatchTheLines_CreatesNoOrder()
    {
        var request = await ConsumeAsync(Checkout() with { TotalPrice = 999 }, expectFault: true);

        Assert.Null(request);
    }

    [Fact]
    public async Task Consume_WithoutLines_CreatesNoOrder()
    {
        var request = await ConsumeAsync(Checkout() with { Items = [], TotalPrice = 0 }, expectFault: true);

        Assert.Null(request);
    }

    private static BasketCheckoutSubmittedIntegrationEvent Checkout()
    {
        var address = new CheckoutAddress(
            "Mustafa", "Akcakaya", "mustafa@example.com", "Bahcelievler No:4", "Turkey", "Istanbul", "34180");

        return new BasketCheckoutSubmittedIntegrationEvent
        {
            UserName = "mustafa",
            CustomerId = Guid.NewGuid(),
            TotalPrice = 1_025,
            Items =
            [
                new BasketCheckoutLine(IphoneId, "IPhone X", 2, 500),
                new BasketCheckoutLine(CaseId, "Phone case", 1, 25)
            ],
            ShippingAddress = address,
            BillingAddress = address,
            Payment = new CheckoutPayment("pt_0123456789abcdef", "4444", 1)
        };
    }

    private static async Task<object?> ConsumeAsync(
        BasketCheckoutSubmittedIntegrationEvent message,
        bool expectFault = false)
    {
        var sender = new RecordingSender();

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ISender>(sender)
            .AddMassTransitTestHarness(configurator =>
            {
                configurator.AddConsumer<BasketCheckoutSubmittedHandler>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(message);

            Assert.True(await harness.GetConsumerHarness<BasketCheckoutSubmittedHandler>()
                .Consumed.Any<BasketCheckoutSubmittedIntegrationEvent>());
            Assert.Equal(expectFault,
                await harness.Published.Any<Fault<BasketCheckoutSubmittedIntegrationEvent>>());

            return sender.Request;
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class RecordingSender : ISender
    {
        public object? Request { get; private set; }

        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            Request = request;

            return Task.FromResult((TResponse)(object)new CreateOrderResult(Guid.NewGuid()));
        }

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Request = request;
            return Task.CompletedTask;
        }

        public Task<object?> Send(
            object request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult<object?>(null);
        }

        public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public async IAsyncEnumerable<object?> CreateStream(
            object streamRequest,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
