using System.Runtime.CompilerServices;
using Serilog;
using Wrap.CrazyEmoji.Api.Bootstraps;
using Wrap.CrazyEmoji.Api.Extensions;

[assembly: InternalsVisibleTo("Wrap.CrazyEmoji.UnitTests")]

try
{
    var allowSpecificOrigins = "_allowSpecificOrigins";
    var builder = WebApplication
        .CreateBuilder(args)
        .SetupObservability();
    
    builder.Services.AddCors(options => {
        options.AddPolicy(name: allowSpecificOrigins, policy => {
            policy.WithOrigins("http://localhost:4200/")
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });
    builder.Services.AddControllers();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi();

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

    app.UseCors(allowSpecificOrigins);

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