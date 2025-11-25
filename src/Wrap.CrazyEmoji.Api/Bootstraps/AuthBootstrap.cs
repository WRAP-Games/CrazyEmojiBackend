using Microsoft.AspNetCore.Identity;
using Wrap.CrazyEmoji.Api.Data;

namespace Wrap.CrazyEmoji.Api.Bootstraps;

internal static class AuthBootstrap
{
    internal static IServiceCollection RegisterAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
                options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
            })
            .AddBearerToken(IdentityConstants.BearerScheme);

        services
            .AddAuthorization()
            .AddIdentityCore<IdentityUser>(options =>
            {
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireDigit = false;
            })
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddApiEndpoints();

        return services;
    }
}