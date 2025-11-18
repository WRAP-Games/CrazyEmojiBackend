using Microsoft.EntityFrameworkCore;
using Wrap.CrazyEmoji.Api.Data;

namespace Wrap.CrazyEmoji.Api.Bootstraps;

internal static class DatabaseBootstrap
{
    internal static IServiceCollection RegisterDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string 'DefaultConnection' not found.");
        }

        services.AddDbContext<GameDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        return services;
    }
}