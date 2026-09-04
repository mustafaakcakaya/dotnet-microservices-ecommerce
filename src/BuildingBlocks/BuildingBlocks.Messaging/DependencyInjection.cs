using System.Reflection;
using BuildingBlocks.Messaging.Configuration;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Publishing;
using Confluent.Kafka;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the publish side of the message broker selected by
    /// <c>MessageBroker:Provider</c> (RabbitMq or Kafka). Callers depend only on
    /// <see cref="IIntegrationEventPublisher"/>, so switching brokers is a
    /// configuration change, not a code change.
    /// </summary>
    public static IServiceCollection AddMessageBroker(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly? consumersAssembly = null)
    {
        var section = configuration.GetSection(MessageBrokerOptions.SectionName);
        services.Configure<MessageBrokerOptions>(section);

        var options = section.Get<MessageBrokerOptions>() ?? new MessageBrokerOptions();

        services.AddSingleton<IntegrationEventTypeRegistry>();

        return options.Provider switch
        {
            MessageBrokerProvider.Kafka => services.AddKafkaPublisher(),
            MessageBrokerProvider.RabbitMq => services.AddRabbitMqBus(options.RabbitMq, consumersAssembly),
            _ => throw new InvalidOperationException($"Unsupported message broker provider '{options.Provider}'.")
        };
    }

    /// <summary>
    /// Registers the SQL Server inbox used to deduplicate at-least-once deliveries.
    /// Required by any service that registers consumers.
    /// </summary>
    public static IServiceCollection AddSqlServerInbox(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<IInboxStore>(new SqlServerInboxStore(connectionString));

        return services;
    }

    private static IServiceCollection AddRabbitMqBus(
        this IServiceCollection services,
        RabbitMqOptions rabbitMq,
        Assembly? consumersAssembly)
    {
        services.AddMassTransit(config =>
        {
            config.SetKebabCaseEndpointNameFormatter();

            if (consumersAssembly is not null)
            {
                config.AddConsumers(consumersAssembly);
            }

            config.UsingRabbitMq((context, configurator) =>
            {
                configurator.Host(new Uri(rabbitMq.Host), host =>
                {
                    host.Username(rabbitMq.UserName);
                    host.Password(rabbitMq.Password);
                });

                if (consumersAssembly is not null)
                {
                    // Every consumer is deduplicated on MessageId; see AddSqlServerInbox.
                    configurator.UseConsumeFilter(typeof(IdempotentConsumeFilter<>), context);
                }

                configurator.ConfigureEndpoints(context);
            });
        });

        // MassTransit registers its own "masstransit-bus" health check with the hosted bus.
        services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();

        return services;
    }

    private static IServiceCollection AddKafkaPublisher(this IServiceCollection services)
    {
        services.AddSingleton<IProducer<string, string>>(serviceProvider =>
        {
            var kafka = serviceProvider.GetRequiredService<IOptions<MessageBrokerOptions>>().Value.Kafka;

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = kafka.BootstrapServers,
                EnableIdempotence = kafka.EnableIdempotence,
                Acks = Enum.Parse<Acks>(kafka.Acks, ignoreCase: true),
                MessageTimeoutMs = kafka.MessageTimeoutMs
            };

            return new ProducerBuilder<string, string>(producerConfig).Build();
        });

        services.AddSingleton<IIntegrationEventPublisher, KafkaIntegrationEventPublisher>();

        services.AddHealthChecks()
            .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready", "broker"]);

        return services;
    }
}
