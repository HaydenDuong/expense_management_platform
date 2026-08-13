using Microsoft.EntityFrameworkCore;
using expense_management_app.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using expense_management_app.Models.Identity;
using expense_management_app.Options;
using expense_management_app.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
        
        // Register ASP.NET Core PasswordHasher
        services.AddScoped<PasswordHasher<AppUser>>();

        // Register Jwt Service
        services.Configure<JwtOptions>(
            configuration.GetSection("Jwt")
        );

        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        // Loaded "Jwt" configuration value from "appsetting.Development.json into variable "jwtOptions" for simple interaction in the next step
        var jwtOptions = configuration
            .GetSection("Jwt")
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing");

        // Register Jwt Authentication Service
        // "Teaches" DI how JWT validation works
        services
            
            // When an endpoint needs authentication, use Bearer token authentication by default
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            
            // This tells ASP.NET how to handle this Bearer token
            .AddJwtBearer(options =>
            {
                // This tell ASP.NET to keep claim names exactly as they appear in the JWT
                // Because ASP.NET can sometimes maps the defined claim names into older Microsoft claim names, like:
                // Further complicate the process
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Set this to "true" = making sure ASP.NET does not skip this validation rule
                    // Set to "false" = ASP.NET will skips that validation rule
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,

                    // For rejecting expired tokens
                    ValidateLifetime = true,

                    // Verify the token signature using the configured value of "Secret" key in "appsettings.Development.json"
                    // This is how the API knows:
                    // This token was created by someone who knows the "Secret" key.
                    // And this token was not modified after being issued
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.Secret)
                    ),

                    // Do not allow extra grace time after token expiry
                    // Useful in distributed system, but not in the current setting
                    ClockSkew = TimeSpan.Zero
                };
            });

        // Allow the adding of [Authorize] to an HTTP endpoint
        services.AddAuthorization();

        return services;
    }
}