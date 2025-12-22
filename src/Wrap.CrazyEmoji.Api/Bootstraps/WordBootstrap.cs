using Wrap.CrazyEmoji.Api.Services;
using Wrap.CrazyEmoji.Api.Abstractions;

namespace Wrap.CrazyEmoji.Api.Bootstraps;

public static class WordBootstrap
{
    public static IServiceCollection AddDbWordService(this IServiceCollection services)
    {
        services.AddSingleton<IDbWordService, DbWordService>();

        return services;
    }
}
