namespace Ordering.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        // API services

        return services;
    }

    public static WebApplication UseApiServices(this WebApplication app)
    {
        // API request pipeline

        return app;
    }
}
