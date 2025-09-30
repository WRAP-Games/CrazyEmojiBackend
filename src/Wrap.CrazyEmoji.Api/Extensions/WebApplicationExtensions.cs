using Scalar.AspNetCore;
using Wrap.CrazyEmoji.Api.GameLogic;

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

        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.MapHub<RoomHub>("/roomHub");
        app.MapControllers();

        return app;
    }
}