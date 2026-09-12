using BuildingBlocks.Messaging;

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
{
    opts.Connection(builder.Configuration.GetConnectionString("Database")!);
    opts.Schema.For<ShoppingCart>().Identity(x => x.UserName);
}).UseLightweightSessions();

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
