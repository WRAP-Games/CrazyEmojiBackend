using System.Reflection;
using Mapster;

namespace Wrap.CrazyEmoji.Api.Bootstraps;

internal static class MapsterBootstrap
{
    internal static IServiceCollection RegisterMapster(this IServiceCollection services)
    {
        services.AddMapster();

        var config = TypeAdapterConfig.GlobalSettings;

        config.Scan(Assembly.GetExecutingAssembly());

        config.Compile();

        services.AddSingleton(config);

        return services;
    }
}