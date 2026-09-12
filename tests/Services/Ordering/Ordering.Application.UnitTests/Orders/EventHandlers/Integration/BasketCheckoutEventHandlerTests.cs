using BuildingBlocks.Messaging.Events;
using MassTransit;
using MassTransit.Testing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Orders.Commands.CreateOrder;
using Ordering.Application.Orders.EventHandlers.Integration;

namespace Ordering.Application.UnitTests.Orders.EventHandlers.Integration;

public class BasketCheckoutEventHandlerTests
{
    [Fact]
    public async Task Consume_ShouldMapEventAndSendCreateOrderCommand()
    {
        var sender = new RecordingSender();

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ISender>(sender)
            .AddMassTransitTestHarness(configurator =>
            {
                configurator.AddConsumer<BasketCheckoutEventHandler>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var message = new BasketCheckoutEvent
        {
            UserName = "mustafa",
            CustomerId = Guid.NewGuid(),
            TotalPrice = 1_400,
            FirstName = "Mustafa",
            LastName = "Akçakaya",
            EmailAddress = "mustafa@example.com",
            AddressLine = "Test address",
            Country = "Türkiye",
            State = "İstanbul",
            ZipCode = "34000",
            CardName = "Mustafa Akçakaya",
            CardNumber = "4111111111111111",
            Expiration = "12/30",
            CVV = "123",
            PaymentMethod = 1
        };

        await harness.Bus.Publish(message);

        Assert.True(await harness.Consumed.Any<BasketCheckoutEvent>());
        Assert.True(await harness.GetConsumerHarness<BasketCheckoutEventHandler>()
            .Consumed.Any<BasketCheckoutEvent>());

        var command = Assert.IsType<CreateOrderCommand>(sender.Request);
        Assert.Equal(message.CustomerId, command.Order.CustomerId);
        Assert.Equal(message.UserName, command.Order.OrderName);
        Assert.Equal(message.FirstName, command.Order.ShippingAddress.FirstName);
        Assert.Equal(message.CardName, command.Order.Payment.CardName);
        Assert.Equal(2, command.Order.OrderItems.Count);
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
