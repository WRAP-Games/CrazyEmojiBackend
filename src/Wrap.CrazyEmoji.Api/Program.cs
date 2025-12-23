using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Identity;
using Serilog;
using Wrap.CrazyEmoji.Api.Abstractions;
using Wrap.CrazyEmoji.Api.Bootstraps;
using Wrap.CrazyEmoji.Api.Extensions;
using Wrap.CrazyEmoji.Api.GameLogic;
using Wrap.CrazyEmoji.Api.Infrastructure;

[assembly: InternalsVisibleTo("Wrap.CrazyEmoji.UnitTests")]
[assembly: InternalsVisibleTo("Wrap.CrazyEmoji.IntegrationTests")]

try
{
    var builder = WebApplication
        .CreateBuilder(args)
        .SetupObservability();

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services
        .RegisterDatabase(builder.Configuration)
        .AddExceptionHandler<GlobalExceptionHandler>()
        .AddProblemDetails()
        .AddOpenApi()
        .AddScoped<IRoomManager, RoomManager>()
        .AddDbWordService()
        .AddScoped<IPasswordHasher<Wrap.CrazyEmoji.Api.Data.Entities.User>, PasswordHasher<Wrap.CrazyEmoji.Api.Data.Entities.User>>()
        .RegisterMapster()
        .RegisterCors()
        .RegisterSignalR()
        .AddControllers();

    var app = builder.Build();

    Log.Information("Application has been built for {EnvironmentName} environment.", builder.Environment.EnvironmentName);

    app.SetupWebApplication();

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Log.Information("Application is starting up.");
    });

    app.Lifetime.ApplicationStopped.Register(() =>
    {
        Log.Information("Application is shutting down.");
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Failed to start application.");
}
finally
{
    Log.CloseAndFlush();
}