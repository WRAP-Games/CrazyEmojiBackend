using System.Text.Json;
using Microsoft.Extensions.Primitives;

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
        string correlationId;

        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out StringValues headerValues))
        {
            if (headerValues.Count > 1)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/problem+json";
                var error = new { error = "Multiple Correlation-Id headers are not allowed." };
                await context.Response.WriteAsync(JsonSerializer.Serialize(error));
                return;
            }

            correlationId = headerValues.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(correlationId))
            {
                correlationId = Guid.NewGuid().ToString();
                context.Request.Headers[CorrelationIdHeader] = correlationId;
            }
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[CorrelationIdHeader] = correlationId;
        }

        context.Items[CorrelationIdHeader] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}