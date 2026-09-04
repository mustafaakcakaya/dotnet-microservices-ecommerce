using BuildingBlocks.Messaging;
using Ordering.Worker.Cdc;
using Ordering.Worker.Checkpoints;
using Ordering.Worker.Data;
using Ordering.Worker.Failures;
using Ordering.Worker.Initialization;
using Ordering.Worker.Processing;

namespace Ordering.Worker;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")!;

        services.Configure<OutboxCdcOptions>(configuration.GetSection(OutboxCdcOptions.SectionName));

        services.AddSingleton(new SqlConnectionFactory(connectionString));
        services.AddSingleton<ICdcReadinessWaiter, CdcReadinessWaiter>();
        services.AddSingleton<ICdcOutboxReader, SqlCdcOutboxReader>();
        services.AddSingleton<IOutboxCheckpointStore, SqlOutboxCheckpointStore>();
        services.AddSingleton<IOutboxPublishFailureStore, SqlOutboxPublishFailureStore>();
        services.AddSingleton<OutboxCdcProcessor>();

        // Provider (RabbitMq / Kafka) comes from the MessageBroker configuration
        // section; the worker itself only knows IIntegrationEventPublisher.
        services.AddMessageBroker(configuration);

        services.AddHealthChecks()
            .AddSqlServer(connectionString);

        services.AddHostedService<OutboxCdcWorker>();

        return services;
    }
}
