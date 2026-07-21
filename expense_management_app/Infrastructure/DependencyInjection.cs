using Microsoft.EntityFrameworkCore;
using expense_management_app.Infrastructure.Persistence;

namespace expense_management_app.Infrastructure;

// This class holds extension methods for service registration
// Because this is an Infrastructure DI, so:
// Suitable for services that related to: database, storage, messaging, external services, file systems, cloud providers
public static class DependencyInjection
{
    // This method adds insfrastructure services and returns "IServiceCollection" so calls can be chained
    public static IServiceCollection AddInfrastructure(

        // This allow the call of: builder.Services.AddInfrastructure(builder.Configuration)
        // instead of: DependencyInjection.AddInfrastructure(builder.Services, builder.Configuration)
        this IServiceCollection services,

        // This must be pass-in because infrastructure layer needs config values, such as the PostgreSQL connection string
        IConfiguration configuration)
    {
        // Registers AppDbContext with option using PostgreSQL as a DI
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Postgres"));
        });

        // Registers Health Check service for connection to PostgreSQL DB from local machine.
        // AddDbContextCheck verifies that EF Core can connect to the configured PostgreSQL database
        // This marked complete for task: "Expose /health and include a DB connectivity check inside it"
        services
            .AddHealthChecks()
            .AddDbContextCheck<AppDbContext>();
        
        return services;
    }
}