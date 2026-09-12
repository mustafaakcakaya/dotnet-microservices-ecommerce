using Ordering.API;
using Ordering.Application;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Data.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplicationServices(builder.Configuration)
    .AddInfrastructureServices(builder.Configuration)
    .AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseApiServices();

// Migrating and seeding is driven by an explicit switch rather than inferred
// from the environment name; it defaults to the environment so local runs need
// no configuration. Override with Ordering:Seed (or Ordering__Seed).
if (app.Configuration.GetValue<bool?>("Ordering:Seed") ?? app.Environment.IsDevelopment())
{
    await app.InitialiseDatabaseAsync();
}

app.Run();
