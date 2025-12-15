using System.Runtime.CompilerServices;
using Serilog;
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
        .RegisterAuth(builder.Configuration)
        .AddExceptionHandler<GlobalExceptionHandler>()
        .AddProblemDetails()
        .AddOpenApi()
        .AddSingleton<RoomManager>()
        .RegisterMapster()
        .RegisterCors()
        .RegisterSignalR()
        .AddControllers();

    //builder.Services.AddDbWordService();

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