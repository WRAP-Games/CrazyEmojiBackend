using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wrap.CrazyEmoji.Api.Data;

namespace Wrap.CrazyEmoji.IntegrationTests.Infrastructure;

public sealed class ApiFixture : IAsyncLifetime
{
    private ApiApplicationFactory _factory = null!;
    private PostgreSqlContainer _container = null!;

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithDatabase("crazyemoji")
            .WithUsername("postgres")
            .WithPassword("postgrespw")
            .Build();

        await _container.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _container.GetConnectionString());

        _factory = new ApiApplicationFactory();

        using var scope = _factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<GameDbContext>()
            .Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _factory.Dispose();
        await _container.DisposeAsync();
    }
}