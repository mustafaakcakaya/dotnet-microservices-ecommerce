using Basket.API.Outbox;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var assembly = typeof(Program).Assembly;
builder.Services.AddCarter();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(assembly);
    config.AddOpenBehavior(typeof(ValidationBehaviour<,>));
    config.AddOpenBehavior(typeof(LoggingBehaviour<,>));
});

builder.Services.AddMarten(opts =>
    BasketStoreConfiguration.Configure(opts, builder.Configuration.GetConnectionString("Database")!))
    .UseLightweightSessions();

builder.Services.TryAddSingleton(TimeProvider.System);

builder.Services.AddScoped<BasketRepository>();
// A missing or malformed value falls back to the default rather than caching forever.
var basketCacheTtl = builder.Configuration.GetValue<TimeSpan?>("CacheSettings:BasketTtl")
                     ?? CachedBasketRepository.DefaultTtl;
builder.Services.AddScoped<IBasketRepository>(sp =>
    new CachedBasketRepository(
        sp.GetRequiredService<BasketRepository>(),
        sp.GetRequiredService<IDistributedCache>(),
        basketCacheTtl,
        sp.GetRequiredService<ILogger<CachedBasketRepository>>()));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis")!;
    // Namespaces every key. Without it the bare user name is the key, which
    // would collide with any other service sharing this Redis instance.
    options.InstanceName = "basket:";
});

builder.Services.AddGrpcClient<DiscountProtoService.DiscountProtoServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:DiscountUrl"]!);
});

// Async communication services
builder.Services.AddMessageBroker(builder.Configuration);

// Ordering consumes the checkout with a MassTransit consumer, so Basket must
// publish through MassTransit. Fail at startup rather than publish into Kafka
// where nothing is listening.
var brokerProvider = builder.Configuration.GetValue<MessageBrokerProvider?>("MessageBroker:Provider")
                     ?? MessageBrokerProvider.RabbitMq;
if (brokerProvider != MessageBrokerProvider.RabbitMq)
{
    throw new InvalidOperationException(
        $"Basket.API publishes checkouts through MassTransit, which Ordering consumes; " +
        $"MessageBroker:Provider must be {MessageBrokerProvider.RabbitMq}, not {brokerProvider}.");
}

// Checkout writes to the outbox; the relay publishes from it.
builder.Services.Configure<BasketOutboxOptions>(
    builder.Configuration.GetSection(BasketOutboxOptions.SectionName));
builder.Services.AddSingleton<ICheckoutEventPublisher, MassTransitCheckoutEventPublisher>();
builder.Services.AddSingleton<BasketOutboxProcessor>();
builder.Services.AddHostedService<BasketOutboxRelay>();

builder.Services.AddExceptionHandler<CustomExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

var app = builder.Build();

// Configure the HTTP request pipeline (same order as Catalog.API).
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Basket API v1"));
    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();
}

app.MapCarter();
app.UseExceptionHandler(options => { });
app.MapHealthChecks("/health",
    new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

app.Run();
