namespace Wrap.CrazyEmoji.Api.Bootstraps;

internal static class CorsBootstrap
{
    internal static IServiceCollection RegisterCors(this IServiceCollection services)
    {
        return services.AddCors(options =>
        {
            options.AddPolicy("ClientCors", builder =>
            {
                builder.WithOrigins("http://localhost:4200")
                    .AllowCredentials()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }
}