using Shopping.Web.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services
    .AddOptions<ShopperOptions>()
    .Bind(builder.Configuration.GetSection(ShopperOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IShopperContext, ShopperContext>();

var gatewayAddress = builder.Configuration["ApiSettings:GatewayAddress"]
    ?? throw new InvalidOperationException("ApiSettings:GatewayAddress is required.");

builder.Services.AddRefitClient<ICatalogService>()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(gatewayAddress));

builder.Services.AddRefitClient<IBasketService>()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(gatewayAddress));

builder.Services.AddRefitClient<IOrderingService>()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(gatewayAddress));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();

public partial class Program;
