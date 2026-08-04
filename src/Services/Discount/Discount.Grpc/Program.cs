using Discount.Grpc.Data;
using Discount.Grpc.Models;
using Discount.Grpc.Services;
using Mapster;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Mapster: entity uses Description; proto field is desciption -> Desciption in C#.
TypeAdapterConfig<Coupon, CouponModel>.NewConfig()
    .Map(dest => dest.Desciption, src => src.Description);
TypeAdapterConfig<CouponModel, Coupon>.NewConfig()
    .Map(dest => dest.Description, src => src.Desciption);

// Add services to the container.
builder.Services.AddGrpc();

// Scoped DbContext + scoped gRPC service (singleton gRPC + DbContext causes DI / lifetime errors).
builder.Services.AddDbContext<DiscountContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<DiscountService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMigrations();
app.MapGrpcService<DiscountService>();
app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
