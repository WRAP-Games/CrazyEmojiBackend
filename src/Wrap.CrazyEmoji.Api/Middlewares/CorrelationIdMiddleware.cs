using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Wrap.CrazyEmoji.Api.Middlewares;


public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationIds))
        {
            if (correlationIds.Count > 1)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Multiple Correlation-Id headers are not allowed.");
                return;
            }
        }
        else
        {
            correlationIds = Guid.NewGuid().ToString();
            context.Request.Headers[CorrelationIdHeader] = correlationIds;
        }

        context.Items[CorrelationIdHeader] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId.ToString();
            return Task.CompletedTask;
        });

        await _next(context);
    }
}