using System.Text.Json;
using Microsoft.Extensions.Primitives;

namespace Wrap.CrazyEmoji.Api.Middlewares;


public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _headerName;


    public CorrelationIdMiddleware(RequestDelegate next, string headerName = "Correlation-Id")
    {
        _next = next;
        _headerName = headerName;

    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        if (!context.Request.Headers.TryGetValue(key: _headerName, value: out StringValues headerValues))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[_headerName] = correlationId;
        }
        else
        {
            if (headerValues.Count > 1)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/problem+json";

                var error = new { error = $"Multiple {_headerName} headers are not allowed." };

                await context.Response.WriteAsync(
                    text: JsonSerializer.Serialize(value: error)
                );

                return;
            }

            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[_headerName] = correlationId;
        }

        context.Items[_headerName] = correlationId;

        context.Response.OnStarting(callback: () =>
        {
            context.Response.Headers[_headerName] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}