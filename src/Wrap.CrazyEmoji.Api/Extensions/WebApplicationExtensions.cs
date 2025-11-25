using Microsoft.AspNetCore.Identity;
using Scalar.AspNetCore;
using Wrap.CrazyEmoji.Api.GameLogic;
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

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseHttpsRedirection();
        app.UseExceptionHandler();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapIdentityApi<IdentityUser>();
        app.MapHub<RoomHub>("/roomHub").RequireAuthorization();
        app.MapControllers();

        return app;
    }
}