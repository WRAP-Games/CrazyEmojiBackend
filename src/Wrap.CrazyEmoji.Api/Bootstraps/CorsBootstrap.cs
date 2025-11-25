namespace Wrap.CrazyEmoji.Api.Bootstraps;

internal static class CorsBootstrap
{
    internal static IServiceCollection RegisterCors(this IServiceCollection services)
    {
        return services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }
}