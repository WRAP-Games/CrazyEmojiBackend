using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Services;

namespace Wrap.CrazyEmoji.Api.Bootstraps;

public static class WordBootstrap
{
    public static IServiceCollection AddDbWordService(this IServiceCollection services)
    {
        services.AddScoped<IWordService, DbWordService>();

        return services;
    }
}
