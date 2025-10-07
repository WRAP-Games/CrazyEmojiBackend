using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Services;

namespace Wrap.CrazyEmoji.Api.Bootstraps;

public static class WordBootstrap
{
    public static IServiceCollection AddWordService(this IServiceCollection services)
    {
        services.AddSingleton<IWordService, WordService>();
        return services;
    }
}
