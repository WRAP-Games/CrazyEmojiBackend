using Scalar.AspNetCore;
using Wrap.CrazyEmoji.Api.Middlewares;

namespace Wrap.CrazyEmoji.Api.Extensions;

internal static class WebApplicationExtensions
{
    internal static WebApplication SetupWebApplication(this WebApplication app)
    {
        if (!app.Environment.IsProduction())
        {
            app.MapOpenApi();
            app.MapScalarApiReference("/docs");
        }

        app.UseCorrelationId();
        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.MapControllers();

        return app;
    }
    
    internal static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}