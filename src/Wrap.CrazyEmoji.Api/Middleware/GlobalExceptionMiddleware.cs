using System.Net;
using System.Text.Json;

namespace Wrap.CrazyEmoji.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogError(exception, "Unhandled exception caught by global handler. Method: {Method}, Path: {Path}, TraceId: {TraceId}", context.Request.Method, context.Request.Path, traceId);
            await HandleExceptionAsync(context, exception, traceId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string traceId)
    {
        HttpStatusCode statusCode = exception switch
        {
            ArgumentException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            _ => HttpStatusCode.InternalServerError
        };

        var errorResponse = new
        {
            errorMessage = "An unexpected error occurred.",
            detail = _env.IsDevelopment() ? exception.Message : null,
            stackTrace = _env.IsDevelopment() ? exception.StackTrace : null,
            traceId
        };

        var result = JsonSerializer.Serialize(errorResponse);
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(result);
    }
}