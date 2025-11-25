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
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            })
            .AddIdentityCookies();

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

        return services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "CrazyEmojiAuth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });
    }
}