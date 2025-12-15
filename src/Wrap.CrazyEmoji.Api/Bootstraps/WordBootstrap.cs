using Microsoft.Extensions.DependencyInjection;
using Wrap.CrazyEmoji.Api.Data;
using Wrap.CrazyEmoji.Api.Services;
using Wrap.CrazyEmoji.Api.Abstractions;


namespace Wrap.CrazyEmoji.Api.Bootstraps;

public static class WordBootstrap
{
    public static IServiceCollection AddDbWordService(this IServiceCollection services)
    {
        services.AddScoped<IDbWordService, DbWordService>();

        return services;
    }
}
