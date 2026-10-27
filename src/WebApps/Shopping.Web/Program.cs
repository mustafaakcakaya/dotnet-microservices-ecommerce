using Shopping.Web.Options;
using Shopping.Web.Routing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
    options.Conventions.Add(new KebabCasePageRouteConvention()));
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

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

builder.Services.AddRefitClient<IPaymentService>()
    .ConfigureHttpClient(client => client.BaseAddress = new Uri(gatewayAddress));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// The container has no locale, so formatting fell back to the invariant culture
// and prices rendered with the generic currency sign. Pin the storefront's culture.
const string storefrontCulture = "en-US";
app.UseRequestLocalization(options => options
    .SetDefaultCulture(storefrontCulture)
    .AddSupportedCultures(storefrontCulture)
    .AddSupportedUICultures(storefrontCulture));

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();

public partial class Program;
