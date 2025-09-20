using Serilog;

namespace Wrap.CrazyEmoji.Api.Bootstraps;

internal static class ObservabilityBootstrap
{
    internal static WebApplicationBuilder SetupObservability(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, loggerConfiguration) =>
        {
            loggerConfiguration.ReadFrom.Configuration(context.Configuration);
        });

        return builder;
    }
}