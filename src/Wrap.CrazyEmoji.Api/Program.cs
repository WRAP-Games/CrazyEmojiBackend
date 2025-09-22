using System.Runtime.CompilerServices;
using Microsoft.OpenApi.Models;
using Serilog;
using Wrap.CrazyEmoji.Api.Bootstraps;
using Wrap.CrazyEmoji.Api.Extensions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;


[assembly: InternalsVisibleTo("Wrap.CrazyEmoji.UnitTests")]

try
{
    var builder = WebApplication
        .CreateBuilder(args)
        .SetupObservability();

    builder.Services.AddControllers();
    
    //swagger services
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo()
        {
            Title = "CrazyEmoji API",
            Version = "v1",
            Description = "CrazyEmoji API"
        });
    });
    
    var app = builder.Build();

    Log.Information("Application has been built for {EnvironmentName} environment.", builder.Environment.EnvironmentName);

    app.SetupWebApplication();

    //creates the openAPI json file
    if (builder.Environment.IsDevelopment())
    {
        
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "CrazyEmoji API v1");
            c.RoutePrefix = string.Empty;
        });
    }
    
    

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Log.Information("Application is starting up.");
        
        //saves json file
            try
            {
                
                using var client = new HttpClient();
                var swaggerJson = client.GetStringAsync("http://localhost:5026/swagger/v1/swagger.json").GetAwaiter().GetResult();                
                File.WriteAllText("swagger.json", swaggerJson);
                Log.Information("Swagger JSON OpenAPI saved to swagger.json (fetched via HTTP)");

            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to save swagger.json");
            }
            
        var serverAddressesFeature = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();

        //gets the url for swagger json openAPI upon starting
        if (serverAddressesFeature != null)
        {
            foreach (var address in serverAddressesFeature.Addresses)
            {
                var swaggerUrl = $"{address}/swagger/v1/swagger.json";
                Log.Information("Swagger JSON OpenAPI available at {SwaggerUrl}", swaggerUrl);            }
        }
        
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