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
builder.Services.AddGrpc()
    .AddJsonTranscoding();
builder.Services.AddGrpcSwagger();
builder.Services.AddSwaggerGen();

// Scoped DbContext + scoped gRPC service (singleton gRPC + DbContext causes DI / lifetime errors).
builder.Services.AddDbContext<DiscountContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<DiscountService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMigrations();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Discount gRPC API v1"));
}

app.MapGrpcService<DiscountService>();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
