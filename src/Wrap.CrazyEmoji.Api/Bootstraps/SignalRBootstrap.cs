namespace Wrap.CrazyEmoji.Api.Bootstraps;

internal static class SignalRBootstrap
{
    internal static IServiceCollection RegisterSignalR(this IServiceCollection services)
    {
        services.AddSignalR();

        return services;
    }
}