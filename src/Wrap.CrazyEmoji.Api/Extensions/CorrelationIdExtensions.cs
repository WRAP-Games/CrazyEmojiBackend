using Wrap.CrazyEmoji.Api.Middlewares;

namespace Wrap.CrazyEmoji.Api.Extensions;

public static class CorrelationIdExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }   
}