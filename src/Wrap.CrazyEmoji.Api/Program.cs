using System.Runtime.CompilerServices;
using Serilog;
using Wrap.CrazyEmoji.Api.Bootstraps;
using Wrap.CrazyEmoji.Api.Extensions;
using Wrap.CrazyEmoji.Api.Infrastructure;
using Wrap.CrazyEmoji.Api.Services;

[assembly: InternalsVisibleTo("Wrap.CrazyEmoji.UnitTests")]

try
{
    var builder = WebApplication
        .CreateBuilder(args)
        .SetupObservability();

    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services
        .RegisterAuth(builder.Configuration)
        .AddExceptionHandler<GlobalExceptionHandler>()
        .AddProblemDetails()
        .AddOpenApi()
        .RegisterMapster()
        .RegisterCors()
        .RegisterSignalR()
        .AddWordService()
        .AddControllers();

    var app = builder.Build();

    Log.Information("Application has been built for {EnvironmentName} environment.", builder.Environment.EnvironmentName);

    app.SetupWebApplication();
    
    var wordService = new WordService();
    using var stream = File.OpenRead("words.txt");
    await wordService.LoadWordsAsync(stream);
    builder.Services.AddSingleton(wordService);

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

public partial class Program { }