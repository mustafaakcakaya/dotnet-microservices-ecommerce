using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Discount.Grpc.Data;

public static class Extensions
{
    public static IApplicationBuilder UseMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiscountContext>();
        dbContext.Database.Migrate();

        return app;
    }
}