using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Wrap.CrazyEmoji.Api.Data;
using Wrap.CrazyEmoji.IntegrationTests.Infrastructure.Auth;

namespace Wrap.CrazyEmoji.IntegrationTests.Infrastructure;

public sealed class ApiFixture : IAsyncLifetime
{
    private ApiApplicationFactory _factory = null!;
    private PostgreSqlContainer _container = null!;
    private HttpClientProvider _httpClientProvider = null!;

    private const string RegisterPath = "api/auth/register";
    private const string LoginPath = "api/auth/login";
    private const string RefreshPath = "api/auth/refresh";

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithDatabase("crazyemoji")
            .WithUsername("postgres")
            .WithPassword("postgrespw")
            .Build();

        await _container.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Supabase", _container.GetConnectionString());

        _factory = new ApiApplicationFactory();
        _httpClientProvider = new HttpClientProvider(_factory, RegisterPath, LoginPath, RefreshPath);
        
        await MigrateDatabase();
    }

    public async ValueTask DisposeAsync()
    {
        _factory.Dispose();
        await _container.DisposeAsync();
    }

    internal Task<HttpClient> GetAuthorizedUser1ClientAsync() => _httpClientProvider.GetHttpClientAsync(TestUsers.User1, "http://localhost/", includeAccessToken: true);
    internal Task<HttpClient> GetAuthorizedUser2ClientAsync() => _httpClientProvider.GetHttpClientAsync(TestUsers.User2, "http://localhost/", includeAccessToken: true);
    internal Task<HttpClient> GetAuthorizedUser3ClientAsync() => _httpClientProvider.GetHttpClientAsync(TestUsers.User3, "http://localhost/", includeAccessToken: true);
    internal Task<HttpClient> GetUnauthorizedUser1ClientAsync() => _httpClientProvider.GetHttpClientAsync(TestUsers.User1, "http://localhost/", includeAccessToken: false);
    internal Task<HttpClient> GetUnauthorizedUser2ClientAsync() => _httpClientProvider.GetHttpClientAsync(TestUsers.User2, "http://localhost/", includeAccessToken: false);
    internal Task<HttpClient> GetUnauthorizedUser3ClientAsync() => _httpClientProvider.GetHttpClientAsync(TestUsers.User3, "http://localhost/", includeAccessToken: false);

    private async Task MigrateDatabase()
    {
        using var scope = _factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
            .Database.MigrateAsync();

        // TODO: Enable when GameDbContext is properly set up
        //await scope.ServiceProvider.GetRequiredService<GameDbContext>()
        //    .Database.MigrateAsync();
    }
}